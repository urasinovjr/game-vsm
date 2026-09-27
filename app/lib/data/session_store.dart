import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class LocalIdentity {
  const LocalIdentity({
    required this.token,
    required this.avatar,
    required this.employeeCode,
    required this.depot,
  });

  final String token;
  final int avatar;
  final String employeeCode;
  final String depot;
}

abstract class SessionStore {
  Future<LocalIdentity?> read();
  Future<void> save(LocalIdentity identity);
  Future<void> clear();
}

class SecureSessionStore implements SessionStore {
  SecureSessionStore({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  @override
  Future<LocalIdentity?> read() async {
    final token = await _storage.read(key: 'profile_token');
    if (token == null) return null;
    return LocalIdentity(
      token: token,
      avatar: int.tryParse(await _storage.read(key: 'avatar') ?? '') ?? 0,
      employeeCode: await _storage.read(key: 'employee_code') ?? '0007',
      depot: await _storage.read(key: 'depot') ?? 'Москва',
    );
  }

  @override
  Future<void> save(LocalIdentity identity) async {
    await _storage.write(key: 'profile_token', value: identity.token);
    await _storage.write(key: 'avatar', value: '${identity.avatar}');
    await _storage.write(key: 'employee_code', value: identity.employeeCode);
    await _storage.write(key: 'depot', value: identity.depot);
  }

  @override
  Future<void> clear() async {
    for (final key in ['profile_token', 'avatar', 'employee_code', 'depot']) {
      await _storage.delete(key: key);
    }
  }
}
