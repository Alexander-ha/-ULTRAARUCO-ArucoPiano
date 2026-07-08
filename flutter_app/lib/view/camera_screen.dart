import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:camera/camera.dart';
import 'package:flora_nano_aruco/utils.dart';
import 'package:flora_nano_aruco/models/app_data.dart';
import 'package:http/http.dart' as http;
import 'package:opencv_core/opencv.dart' as cv;
import 'package:flutter/services.dart';
import '../camera/aruco_bridge.dart';

class CameraScreen extends StatefulWidget {
  const CameraScreen({super.key});

  @override
  State<CameraScreen> createState() => _CameraScreenState();
}

class _CameraScreenState extends State<CameraScreen> {
  CameraController? _controller;
  bool _isDetecting = false;
  String _detectionResult = 'Waiting for camera...';
  ArucoClassicDetector? _detector;

  ui.Image? _displayImage;

  final AppData _appData = AppData();
  DateTime _lastSendTime = DateTime.now().subtract(const Duration(seconds: 10));
  final Duration _sendInterval = const Duration(milliseconds: 75);
  bool _isSending = false;

  final _orientations = {
    DeviceOrientation.portraitUp: 0,
    DeviceOrientation.landscapeLeft: 90,
    DeviceOrientation.portraitDown: 180,
    DeviceOrientation.landscapeRight: 270,
  };

  @override
  void initState() {
    super.initState();
    _initCamera();
    _detector = ArucoClassicDetector();
  }

  Future<void> _initCamera() async {
    try {
      final cameras = await availableCameras();
      if (cameras.isEmpty) {
        setState(() {
          _detectionResult = 'No cameras found';
        });
        return;
      }
      _controller = CameraController(
        cameras[0],
        ResolutionPreset.medium,
        imageFormatGroup: ImageFormatGroup.yuv420,
      );
      await _controller!.initialize();
      _controller!.startImageStream(_processCameraImage);
      setState(() {});
    } on CameraException catch (e) {
      setState(() {
        _detectionResult = 'Camera error: $e';
      });
    }
  }

  Future<cv.Mat?> _convertImage(CameraImage image) async {
    final Uint8List rgba = yuv420ToRGBA8888(image);
    cv.Mat mat = cv.Mat.fromList(
      image.height,
      image.width,
      cv.MatType.CV_8UC4,
      rgba,
    );

    final sensorOrientation = _controller?.description.sensorOrientation;
    var rotationCompensation = _orientations[_controller?.value.deviceOrientation];
    if (rotationCompensation != null && sensorOrientation != null) {
      if (_controller?.description.lensDirection == CameraLensDirection.front) {
        rotationCompensation = (sensorOrientation + rotationCompensation) % 360;
      } else {
        rotationCompensation = (sensorOrientation - rotationCompensation + 360) % 360;
      }
      switch (rotationCompensation) {
        case 90:
          await cv.rotateAsync(mat, cv.ROTATE_90_CLOCKWISE, dst: mat);
          break;
        case 180:
          await cv.rotateAsync(mat, cv.ROTATE_180, dst: mat);
          break;
        case 270:
          await cv.rotateAsync(mat, cv.ROTATE_90_COUNTERCLOCKWISE, dst: mat);
          break;
      }
    }

    cv.Mat bgr = await cv.cvtColorAsync(mat, cv.COLOR_RGBA2BGR);
    mat.dispose();
    return bgr;
  }

  Future<ui.Image> _rawToUiImage(Uint8List bytes, int width, int height) async {
    final completer = Completer<ui.Image>();
    ui.decodeImageFromPixels(
      bytes,
      width,
      height,
      ui.PixelFormat.rgba8888,
          (ui.Image image) {
        completer.complete(image);
      },
    );
    return completer.future;
  }

  void _processCameraImage(CameraImage image) {
    if (_isDetecting || _detector == null) return;
    _isDetecting = true;

    _convertImage(image).then((cv.Mat? bgr) async {
      if (bgr == null) {
        _isDetecting = false;
        return;
      }

      try {
        // Используем новый детектор из aruco_bridge
        final result = _detector!.detect(bgr.data, bgr.width, bgr.height);

        String msg;
        if (result.markers.isNotEmpty) {
          msg = 'Markers (${result.markers.length}):\n';
          for (var marker in result.markers) {
            // Конвертируем Offset в Point для совместимости с opencv
            final corners = marker['corners'] as List<Offset>;
            final id = marker['id'] as int;

            msg += 'ID $id: (${corners.map((c) => '(${c.dx.toStringAsFixed(1)},${c.dy.toStringAsFixed(1)})').join(' ')})';

            // Рисуем маркеры на BGR
            for (int i = 0; i < 4; i++) {
              final p1 = corners[i];
              final p2 = corners[(i + 1) % 4];
              cv.line(bgr, cv.Point(p1.dx.toInt(), p1.dy.toInt()),
                  cv.Point(p2.dx.toInt(), p2.dy.toInt()),
                  cv.Scalar(0, 255, 0),
                  thickness: 3);
            }
            final centerX = corners.map((c) => c.dx).reduce((a, b) => a + b) / 4;
            final centerY = corners.map((c) => c.dy).reduce((a, b) => a + b) / 4;
            cv.putText(bgr, 'ID: $id',
                cv.Point(centerX.toInt() - 20, centerY.toInt() - 10),
                cv.FONT_HERSHEY_SIMPLEX, 0.8, cv.Scalar(0, 255, 255),
                thickness: 2);
          }

          msg += '\nTime: ${result.processingTimeMs.toStringAsFixed(2)} ms';
          _trySendData(result.markers);
        } else {
          msg = 'No markers detected (${result.processingTimeMs.toStringAsFixed(2)} ms)';
        }

        // Конвертируем в RGBA и создаём ui.Image
        cv.Mat rgba = await cv.cvtColorAsync(bgr, cv.COLOR_BGR2RGBA);
        final imageBytes = rgba.data;
        final width = rgba.width;
        final height = rgba.height;
        rgba.dispose();

        final ui.Image image = await _rawToUiImage(imageBytes, width, height);

        if (mounted) {
          setState(() {
            if (_displayImage != null) {
              _displayImage!.dispose();
            }
            _displayImage = image;
            _detectionResult = msg;
          });
        } else {
          image.dispose();
        }
      } catch (e) {
        print('Detection error: $e');
      } finally {
        bgr.dispose();
        _isDetecting = false;
      }
    }).catchError((e) {
      print('Conversion error: $e');
      _isDetecting = false;
    });
  }

  Future<void> _trySendData(List<Map<String, dynamic>> markers) async {
    final now = DateTime.now();
    if (now.difference(_lastSendTime) < _sendInterval) return;
    if (_isSending) return;
    if (_appData.serverIp.isEmpty || _appData.serverPort.isEmpty) {
      return;
    }

    final Map<String, dynamic> payload = {};

    // Вспомогательная функция для поиска маркера по ID
    Map<String, dynamic>? findMarker(int id) {
      try {
        return markers.firstWhere((m) => m['id'] == id);
      } catch (_) {
        return null;
      }
    }

    if (_appData.leftHandId.isNotEmpty) {
      final id = int.tryParse(_appData.leftHandId);
      if (id != null) {
        final marker = findMarker(id);
        if (marker != null) {
          final corners = marker['corners'] as List<Offset>;
          payload['left_hand'] = {
            'id': id,
            'corners': corners.map((c) => {'x': c.dx, 'y': c.dy}).toList(),
          };
        } else {
          payload['left_hand'] = null;
        }
      }
    }

    if (_appData.rightHandId.isNotEmpty) {
      final id = int.tryParse(_appData.rightHandId);
      if (id != null) {
        final marker = findMarker(id);
        if (marker != null) {
          final corners = marker['corners'] as List<Offset>;
          payload['right_hand'] = {
            'id': id,
            'corners': corners.map((c) => {'x': c.dx, 'y': c.dy}).toList(),
          };
        } else {
          payload['right_hand'] = null;
        }
      }
    }

    if (_appData.pianoId.isNotEmpty) {
      final id = int.tryParse(_appData.pianoId);
      if (id != null) {
        final marker = findMarker(id);
        if (marker != null) {
          final corners = marker['corners'] as List<Offset>;
          payload['piano'] = {
            'id': id,
            'corners': corners.map((c) => {'x': c.dx, 'y': c.dy}).toList(),
          };
        } else {
          payload['piano'] = null;
        }
      }
    }

    if (_appData.extraKeys.isNotEmpty) {
      final extra = <String, dynamic>{};
      _appData.extraKeys.forEach((keyNumber, idStr) {
        final id = int.tryParse(idStr);
        if (id != null) {
          final marker = findMarker(id);
          if (marker != null) {
            final corners = marker['corners'] as List<Offset>;
            extra['key_$keyNumber'] = {
              'id': id,
              'corners': corners.map((c) => {'x': c.dx, 'y': c.dy}).toList(),
            };
          } else {
            extra['key_$keyNumber'] = null;
          }
        }
      });
      if (extra.isNotEmpty) {
        payload['extra_keys'] = extra;
      }
    }

    bool hasData = false;
    payload.forEach((key, value) {
      if (value != null) hasData = true;
    });
    ///if (!hasData) {
    ///  return;
    ///}

    _isSending = true;
    _lastSendTime = now;

    final url = 'http://${_appData.serverIp}:${_appData.serverPort}/api/aruco_data';
    try {
      final response = await http.post(
        Uri.parse(url),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      ).timeout(const Duration(seconds: 3));

      if (response.statusCode == 200 || response.statusCode == 201) {
        print('✅ Data sent successfully');
      } else {
        print('❌ Server error: ${response.statusCode}');
      }
    } catch (e) {
      print('❌ Send error: $e');
    } finally {
      _isSending = false;
    }
  }

  @override
  void dispose() {
    _controller?.stopImageStream();
    _controller?.dispose();
    _detector?.dispose();
    _displayImage?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('ArUco Detector'),
        backgroundColor: Colors.blue,
        foregroundColor: Colors.white,
      ),
      body: Column(
        children: [
          Expanded(
            flex: 3,
            child: _displayImage != null
                ? RawImage(
              image: _displayImage,
              fit: BoxFit.contain,
            )
                : _controller != null && _controller!.value.isInitialized
                ? CameraPreview(_controller!)
                : const Center(child: CircularProgressIndicator()),
          ),
          Expanded(
            flex: 1,
            child: Container(
              padding: const EdgeInsets.all(12),
              color: Colors.grey.shade100,
              child: SingleChildScrollView(
                child: Text(
                  _detectionResult,
                  style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w500),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}