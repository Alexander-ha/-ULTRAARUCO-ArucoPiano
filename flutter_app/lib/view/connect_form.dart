import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:flora_nano_aruco/models/app_data.dart';
import 'package:flora_nano_aruco/view/aruco_form.dart';

class ConnectForm extends StatefulWidget {
  const ConnectForm({super.key});

  @override
  State<ConnectForm> createState() => _ConnectFormState();
}

class _ConnectFormState extends State<ConnectForm> {
  final TextEditingController _ipController = TextEditingController();
  final TextEditingController _portController = TextEditingController();
  bool _isLoading = false;
  String _statusMessage = '';
  Color _statusColor = Colors.grey;

  Future<void> _checkConnection() async {
    final String ip = _ipController.text.trim();
    final String port = _portController.text.trim();

    if (ip.isEmpty) {
      setState(() {
        _statusMessage = 'enter the ip-address';
        _statusColor = Colors.red;
      });
      return;
    }

    if (port.isEmpty) {
      setState(() {
        _statusMessage = 'enter the port';
        _statusColor = Colors.red;
      });
      return;
    }

    setState(() {
      _isLoading = true;
      _statusMessage = 'checking connection to $ip:$port...';
      _statusColor = Colors.orange;
    });

    try {
      final url = Uri.parse('http://$ip:$port/');
      final response = await http.get(url).timeout(const Duration(seconds: 5));

      if (response.statusCode == 200) {
        final appData = AppData();
        appData.serverIp = ip;
        appData.serverPort = port;

        setState(() {
          _statusMessage = '✅ connected to $ip:$port';
          _statusColor = Colors.green;
          _isLoading = false;
        });

        Future.delayed(const Duration(milliseconds: 500), () {
          if (mounted) {
            Navigator.pushReplacement(
              context,
              MaterialPageRoute(
                builder: (context) => const ArUcoForm(),
              ),
            );
          }
        });
      } else {
        setState(() {
          _statusMessage = '❌ server returned: ${response.statusCode}';
          _statusColor = Colors.red;
          _isLoading = false;
        });
      }
    } catch (e) {
      setState(() {
        _statusMessage = '❌ cannot connect to $ip:$port\nCheck if server is running';
        _statusColor = Colors.red;
        _isLoading = false;
      });
      print('Connection error: $e');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Connect to Server'),
        backgroundColor: Colors.blue,
        foregroundColor: Colors.white,
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20.0),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(
                Icons.settings_ethernet,
                size: 60,
                color: Colors.blue,
              ),
              const SizedBox(height: 20),
              TextField(
                controller: _ipController,
                decoration: const InputDecoration(
                  labelText: 'Server IP-address',
                  hintText: '192.168.0.138',
                  prefixIcon: Icon(Icons.dns),
                  border: OutlineInputBorder(),
                ),
                keyboardType: TextInputType.number,
              ),
              const SizedBox(height: 15),
              TextField(
                controller: _portController,
                decoration: const InputDecoration(
                  labelText: 'Port',
                  hintText: '8081',
                  prefixIcon: Icon(Icons.settings),
                  border: OutlineInputBorder(),
                ),
                keyboardType: TextInputType.number,
              ),
              const SizedBox(height: 20),
              if (_statusMessage.isNotEmpty)
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: _statusColor.withOpacity(0.1),
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: _statusColor),
                  ),
                  child: Text(
                    _statusMessage,
                    style: TextStyle(
                      color: _statusColor,
                      fontSize: 14,
                      fontWeight: FontWeight.bold,
                    ),
                    textAlign: TextAlign.center,
                  ),
                ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                height: 50,
                child: ElevatedButton(
                  onPressed: _isLoading ? null : _checkConnection,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.blue,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
                  child: _isLoading
                      ? const CircularProgressIndicator(color: Colors.white)
                      : const Text(
                    'check connection',
                    style: TextStyle(fontSize: 16),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  void dispose() {
    _ipController.dispose();
    _portController.dispose();
    super.dispose();
  }
}