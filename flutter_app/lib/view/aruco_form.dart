///lib/view/aruco_form.dart
import 'package:flutter/material.dart';
import 'package:flora_nano_aruco/models/app_data.dart';

class ArUcoForm extends StatefulWidget {
  const ArUcoForm({super.key});

  @override
  State<ArUcoForm> createState() => _ArUcoFormState();
}

class _ArUcoFormState extends State<ArUcoForm> {
  final TextEditingController _leftHandController = TextEditingController();
  final TextEditingController _rightHandController = TextEditingController();
  final TextEditingController _pianoController = TextEditingController();
  final List<Map<String, dynamic>> _extraKeys = [];
  int _nextKeyNumber = 1;

  @override
  void initState() {
    super.initState();
    // Загружаем сохранённые данные при открытии формы
    final appData = AppData();
    _leftHandController.text = appData.leftHandId;
    _rightHandController.text = appData.rightHandId;
    _pianoController.text = appData.pianoId;

    // Загружаем дополнительные клавиши
    appData.extraKeys.forEach((number, id) {
      _extraKeys.add({
        'number': number,
        'controller': TextEditingController(text: id),
      });
      if (number >= _nextKeyNumber) {
        _nextKeyNumber = number + 1;
      }
    });
  }

  void _addKey() {
    setState(() {
      _extraKeys.add({
        'number': _nextKeyNumber,
        'controller': TextEditingController(),
      });
      _nextKeyNumber++;
    });
  }

  void _removeKey(int index) {
    setState(() {
      _extraKeys[index]['controller']?.dispose();
      _extraKeys.removeAt(index);
    });
  }

  void _saveData() {
    final appData = AppData();

    // Сохраняем данные
    appData.leftHandId = _leftHandController.text.trim();
    appData.rightHandId = _rightHandController.text.trim();
    appData.pianoId = _pianoController.text.trim();

    // Сохраняем дополнительные клавиши
    appData.extraKeys = {};
    for (var key in _extraKeys) {
      final number = key['number'] as int;
      final controller = key['controller'] as TextEditingController;
      final id = controller.text.trim();
      if (id.isNotEmpty) {
        appData.extraKeys[number] = id;
      }
    }

    // Выводим все данные для проверки
    appData.printData();

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('✅ Data has been saved!'),
        backgroundColor: Colors.green,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      body: Column(
        children: [
          Container(
            width: double.infinity,
            height: 200,
            decoration: const BoxDecoration(
              color: Colors.blue,
              borderRadius: BorderRadius.only(
                bottomLeft: Radius.circular(20),
                bottomRight: Radius.circular(20),
              ),
            ),
            child: Center(
              child: Image.asset(
                'assets/hands.png',
                height: 150,
                errorBuilder: (context, error, stackTrace) {
                  return const Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(
                        Icons.handshake,
                        size: 80,
                        color: Colors.white,
                      ),
                      SizedBox(height: 10),
                      Text(
                        'hands.png',
                        style: TextStyle(color: Colors.white, fontSize: 16),
                      ),
                    ],
                  );
                },
              ),
            ),
          ),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(20.0),
              child: Column(
                children: [
                  _buildTextField(
                    label: 'Left Hand ID',
                    hint: 'enter left hand ID',
                    controller: _leftHandController,
                    icon: Icons.chevron_left,
                  ),
                  const SizedBox(height: 15),
                  _buildTextField(
                    label: 'Right Hand ID',
                    hint: 'enter right hand ID',
                    controller: _rightHandController,
                    icon: Icons.chevron_right,
                  ),
                  const SizedBox(height: 15),
                  _buildTextField(
                    label: 'Piano ID',
                    hint: 'enter piano id',
                    controller: _pianoController,
                    icon: Icons.piano,
                  ),
                  const SizedBox(height: 15),
                  ..._extraKeys.asMap().entries.map((entry) {
                    int index = entry.key;
                    var keyData = entry.value;
                    return _buildKeyRow(
                      number: keyData['number'] as int,
                      controller: keyData['controller'] as TextEditingController,
                      onRemove: () => _removeKey(index),
                    );
                  }),
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 15.0),
                    child: ElevatedButton.icon(
                      onPressed: _addKey,
                      icon: const Icon(Icons.add),
                      label: const Text('add key'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.blue,
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 20,
                          vertical: 12,
                        ),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(10),
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 20),
                  SizedBox(
                    width: double.infinity,
                    height: 50,
                    child: ElevatedButton(
                      onPressed: _saveData,
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.green,
                        foregroundColor: Colors.white,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(10),
                        ),
                      ),
                      child: const Text(
                        'SAVE',
                        style: TextStyle(fontSize: 18),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTextField({
    required String label,
    required String hint,
    required TextEditingController controller,
    required IconData icon,
  }) {
    return Container(
      decoration: BoxDecoration(
        border: Border.all(color: Colors.grey.shade300),
        borderRadius: BorderRadius.circular(10),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 10),
      child: Row(
        children: [
          Icon(icon, color: Colors.blue),
          const SizedBox(width: 10),
          Expanded(
            child: TextField(
              controller: controller,
              decoration: InputDecoration(
                labelText: label,
                hintText: hint,
                border: InputBorder.none,
              ),
              keyboardType: TextInputType.number,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildKeyRow({
    required int number,
    required TextEditingController controller,
    required VoidCallback onRemove,
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10.0),
      child: Container(
        decoration: BoxDecoration(
          border: Border.all(color: Colors.grey.shade300),
          borderRadius: BorderRadius.circular(10),
        ),
        padding: const EdgeInsets.symmetric(horizontal: 10),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.blue.shade100,
                borderRadius: BorderRadius.circular(5),
              ),
              child: Text(
                'K$number',
                style: const TextStyle(
                  fontWeight: FontWeight.bold,
                  color: Colors.blue,
                ),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: TextField(
                controller: controller,
                decoration: const InputDecoration(
                  hintText: 'enter key ID',
                  border: InputBorder.none,
                ),
                keyboardType: TextInputType.number,
              ),
            ),
            IconButton(
              onPressed: onRemove,
              icon: const Icon(Icons.close, color: Colors.red),
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints(),
            ),
          ],
        ),
      ),
    );
  }

  @override
  void dispose() {
    _leftHandController.dispose();
    _rightHandController.dispose();
    _pianoController.dispose();
    for (var key in _extraKeys) {
      key['controller']?.dispose();
    }
    super.dispose();
  }
}