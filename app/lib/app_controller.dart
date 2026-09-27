import 'package:flutter/foundation.dart';

import 'data/game_launcher.dart';
import 'data/session_store.dart';
import 'data/training_api.dart';

enum AppPage { onboarding, home, day, profile, achievements, leaderboard }

class AppController extends ChangeNotifier {
  AppController({
    required TrainingGateway gateway,
    required SessionStore store,
    required GameLauncher gameLauncher,
  }) : _gateway = gateway,
       _store = store,
       _gameLauncher = gameLauncher;

  final TrainingGateway _gateway;
  final SessionStore _store;
  final GameLauncher _gameLauncher;

  AppPage? page;
  LocalIdentity? identity;
  TrainingProfile? profile;
  List<LeaderboardEntry> leaderboard = const [];
  String? error;
  bool busy = false;

  Future<void> boot() async {
    try {
      identity = await _store.read();
      if (identity == null) {
        page = AppPage.onboarding;
      } else {
        profile = await _gateway.profile(identity!.token);
        page = AppPage.home;
      }
    } on ApiFailure catch (failure) {
      if (failure.unauthorized) {
        await _store.clear();
        identity = null;
        page = AppPage.onboarding;
      } else {
        error = failure.message;
        page = AppPage.home;
      }
    } on Exception {
      error = 'Не удалось открыть учебный профиль.';
      page = AppPage.home;
    }
    notifyListeners();
  }

  Future<void> register({
    required String name,
    required int avatar,
    required String employeeCode,
    required String depot,
  }) async {
    if (busy) return;
    busy = true;
    error = null;
    notifyListeners();
    try {
      final registered = await _gateway.register(name.trim());
      identity = LocalIdentity(
        token: registered.token,
        avatar: avatar,
        employeeCode: employeeCode.trim(),
        depot: depot.trim(),
      );
      await _store.save(identity!);
      page = AppPage.home;
      profile = await _gateway.profile(registered.token);
    } on ApiFailure catch (failure) {
      error = failure.message;
    } on Exception {
      error = 'Не удалось создать учебный профиль.';
    } finally {
      busy = false;
      notifyListeners();
    }
  }

  Future<void> open(AppPage next) async {
    page = next;
    error = null;
    notifyListeners();
    if (next == AppPage.profile || next == AppPage.achievements) {
      await refreshProfile();
    } else if (next == AppPage.leaderboard) {
      await refreshLeaderboard();
    }
  }

  Future<void> refreshProfile() async {
    final token = identity?.token;
    if (token == null || busy) return;
    busy = true;
    notifyListeners();
    try {
      profile = await _gateway.profile(token);
      error = null;
    } on ApiFailure catch (failure) {
      if (failure.unauthorized) {
        await _store.clear();
        identity = null;
        profile = null;
        page = AppPage.onboarding;
      }
      error = failure.message;
    } on Exception {
      error = 'Не удалось обновить данные.';
    } finally {
      busy = false;
      notifyListeners();
    }
  }

  Future<void> refreshLeaderboard() async {
    final token = identity?.token;
    if (token == null || busy) return;
    busy = true;
    notifyListeners();
    try {
      leaderboard = await _gateway.leaderboard(token);
      error = null;
    } on ApiFailure catch (failure) {
      error = failure.message;
    } on Exception {
      error = 'Не удалось загрузить рейтинг.';
    } finally {
      busy = false;
      notifyListeners();
    }
  }

  Future<void> startGame() async {
    final token = identity?.token;
    final profileId = profile?.id;
    if (token == null || profileId == null || busy) {
      error = 'Откройте учебный профиль перед началом смены.';
      notifyListeners();
      return;
    }
    busy = true;
    error = null;
    notifyListeners();
    try {
      await _gameLauncher.open(token: token, profileId: profileId);
    } on Exception {
      error = 'Не удалось открыть игровую смену. Повторите попытку.';
    } finally {
      busy = false;
      notifyListeners();
    }
  }
}
