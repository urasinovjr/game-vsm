import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:game_vsm_app/main.dart' as app;

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('профиль открывается после установки и доступна смена', (
    tester,
  ) async {
    app.main();
    await tester.pumpAndSettle();

    if (find.text('Выберите своего проводника').evaluate().isNotEmpty) {
      final fields = find.byType(TextField);
      await tester.enterText(fields.at(0), 'Тестовый проводник');
      await tester.enterText(fields.at(1), '0007');
      await tester.ensureVisible(find.text('Продолжить'));
      await tester.tap(find.text('Продолжить'));
      await tester.pumpAndSettle();
    }

    expect(find.byKey(const ValueKey('start_game_home')), findsOneWidget);
    expect(find.text('Программа обучения'), findsOneWidget);
    await tester.ensureVisible(find.text('Первый рейс'));
    await tester.tap(find.text('Первый рейс'));
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('start_game_day')), findsOneWidget);
  });
}
