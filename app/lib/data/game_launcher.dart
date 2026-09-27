import 'package:flutter/services.dart';

abstract class GameLauncher {
  Future<void> open({required String token, required String profileId});
}

class AndroidGameLauncher implements GameLauncher {
  static const _channel = MethodChannel('ru.gamevsm.conductor/game');

  @override
  Future<void> open({required String token, required String profileId}) async {
    await _channel.invokeMethod<void>('openGame', {'profileId': profileId});
  }
}
