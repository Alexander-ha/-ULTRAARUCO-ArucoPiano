///lib/models/app_data.dart
class AppData {
  static final AppData _instance = AppData._internal();
  factory AppData() => _instance;
  AppData._internal();

  String serverIp = '';
  String serverPort = '';
  String leftHandId = '';
  String rightHandId = '';
  String pianoId = '';
  Map<int, String> extraKeys = {};

  void clear() {
    serverIp = '';
    serverPort = '';
    leftHandId = '';
    rightHandId = '';
    pianoId = '';
    extraKeys = {};
  }

  void printData() {
    print('=== App Data ===');
    print('Server: $serverIp:$serverPort');
    print('Left Hand ID: $leftHandId');
    print('Right Hand ID: $rightHandId');
    print('Piano ID: $pianoId');
    print('Extra Keys: $extraKeys');
    print('================');
  }
}