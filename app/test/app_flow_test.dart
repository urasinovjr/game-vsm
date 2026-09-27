import 'package:flutter_test/flutter_test.dart';
import 'package:game_vsm_app/app_controller.dart';
import 'package:game_vsm_app/data/session_store.dart';
import 'package:game_vsm_app/data/training_api.dart';
import 'package:game_vsm_app/main.dart';

class MemorySessionStore implements SessionStore {
  LocalIdentity? value;

  @override
  Future<LocalIdentity?> read() async => value;

  @override
  Future<void> save(LocalIdentity identity) async => value = identity;

  @override
  Future<void> clear() async => value = null;
}

class FakeGateway implements TrainingGateway {
  bool unauthorized = false;
  String? registeredName;

  @override
  Future<RegisteredProfile> register(String name) async {
    registeredName = name;
    return const RegisteredProfile(id: '1', token: 'secret');
  }

  @override
  Future<TrainingProfile> profile(String token) async {
    if (unauthorized) {
      throw const ApiFailure('Профиль не найден', unauthorized: true);
    }
    return const TrainingProfile(
      id: '1',
      name: 'Учебный проводник',
      completed: 0,
      achievements: [],
      notifications: ['Первая смена готова к прохождению'],
      competencies: {},
    );
  }

  @override
  Future<List<LeaderboardEntry>> leaderboard(String token) async => const [];
}

void main() {
  test('новый профиль сохраняется, затем открывается программа', () async {
    final store = MemorySessionStore();
    final gateway = FakeGateway();
    final controller = AppController(gateway: gateway, store: store);
    await controller.boot();
    expect(controller.page, AppPage.onboarding);

    await controller.register(
      name: ' Учебный проводник ',
      avatar: 2,
      employeeCode: '0007',
      depot: 'Москва',
    );

    expect(gateway.registeredName, 'Учебный проводник');
    expect(store.value?.token, 'secret');
    expect(controller.page, AppPage.home);
    expect(controller.profile?.name, 'Учебный проводник');
  });

  test('недействительный токен удаляется при запуске', () async {
    final store = MemorySessionStore()
      ..value = const LocalIdentity(
        token: 'old',
        avatar: 0,
        employeeCode: '0007',
        depot: 'Москва',
      );
    final controller = AppController(
      gateway: FakeGateway()..unauthorized = true,
      store: store,
    );
    await controller.boot();

    expect(store.value, isNull);
    expect(controller.page, AppPage.onboarding);
  });

  testWidgets('экран программы открывает первый день, остальные закрыты', (
    tester,
  ) async {
    final controller = AppController(
      gateway: FakeGateway(),
      store: MemorySessionStore(),
    );
    await controller.register(
      name: 'Учебный проводник',
      avatar: 0,
      employeeCode: '0007',
      depot: 'Москва',
    );
    await tester.pumpWidget(TrainingApp(controller: controller));

    expect(find.text('Программа обучения'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('День 5'), 250);
    expect(find.text('День 5'), findsOneWidget);
    await tester.tap(find.text('День 5'));
    expect(controller.page, AppPage.home);
    await tester.ensureVisible(find.text('Первый рейс'));
    await tester.tap(find.text('Первый рейс'));
    await tester.pumpAndSettle();
    expect(find.text('Этапы маршрута'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('Практический рейс скоро станет доступен.'),
      250,
    );
    expect(
      find.text('Практический рейс скоро станет доступен.'),
      findsOneWidget,
    );
  });
}
