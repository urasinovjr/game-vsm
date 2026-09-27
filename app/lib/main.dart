import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'app_controller.dart';
import 'data/session_store.dart';
import 'data/training_api.dart';
import 'ui/app_view.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setSystemUIOverlayStyle(
    const SystemUiOverlayStyle(
      statusBarColor: Color(0xFF07162B),
      statusBarIconBrightness: Brightness.light,
      systemNavigationBarColor: Color(0xFF07162B),
      systemNavigationBarIconBrightness: Brightness.light,
    ),
  );
  final controller = AppController(
    gateway: HttpTrainingGateway(
      baseUrl: const String.fromEnvironment(
        'API_BASE_URL',
        defaultValue: 'http://10.0.2.2:8000',
      ),
    ),
    store: SecureSessionStore(),
  );
  runApp(TrainingApp(controller: controller));
  controller.boot();
}

class TrainingApp extends StatelessWidget {
  const TrainingApp({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'Проводник ВСМ',
    debugShowCheckedModeBanner: false,
    theme: ThemeData(
      useMaterial3: true,
      fontFamily: 'Manrope',
      scaffoldBackgroundColor: const Color(0xFF07162B),
      colorScheme: ColorScheme.fromSeed(
        seedColor: const Color(0xFF1684FF),
        brightness: Brightness.dark,
      ),
    ),
    home: AppView(controller: controller),
  );
}
