import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  const channel = MethodChannel('ru.gamevsm.conductor/training');

  Future<Map<String, dynamic>> call(
    String httpMethod,
    String path, {
    String profileId = '',
    Map<String, dynamic>? payload,
  }) async {
    final raw = await channel.invokeMethod<String>('debugEngine', {
      'httpMethod': httpMethod,
      'path': path,
      'profileId': profileId,
      'payload': payload == null ? '' : jsonEncode(payload),
    });
    return jsonDecode(raw!) as Map<String, dynamic>;
  }

  testWidgets(
    'автономная смена сохраняет решения и не начисляет баллы дважды',
    (tester) async {
      final registered = await call(
        'POST',
        '/profiles',
        payload: {'name': 'Тестовый проводник'},
      );
      expect(registered['ok'], true);
      final id = (registered['body'] as Map<String, dynamic>)['id'] as String;
      var attempt =
          (await call('POST', '/attempts', profileId: id))['body']
              as Map<String, dynamic>;
      final attemptId = attempt['id'] as String;

      Future<Map<String, dynamic>> choose(
        String node,
        String choice,
        String requestId,
      ) async {
        final response = await call(
          'POST',
          '/attempts/$attemptId/actions',
          profileId: id,
          payload: {
            'request_id': requestId,
            'revision': attempt['revision'],
            'node': node,
            'choice': choice,
          },
        );
        expect(response['ok'], true, reason: response['error']?.toString());
        attempt = response['body'] as Map<String, dynamic>;
        return response;
      }

      await choose('brief', 'receive', '00000000-0000-4000-8000-000000000001');
      await choose('board', 'enter', '00000000-0000-4000-8000-000000000002');
      final before = attempt['revision'];
      final ignoreId = '00000000-0000-4000-8000-000000000003';
      final ignored = await choose('inspect', 'ignore', ignoreId);
      expect((attempt['state'] as Map<String, dynamic>)['quality'], 60);
      final duplicate = await call(
        'POST',
        '/attempts/$attemptId/actions',
        profileId: id,
        payload: {
          'request_id': ignoreId,
          'revision': before,
          'node': 'inspect',
          'choice': 'ignore',
        },
      );
      expect(duplicate['ok'], true);
      expect(
        (duplicate['body'] as Map<String, dynamic>)['revision'],
        (ignored['body'] as Map<String, dynamic>)['revision'],
      );
      final conflict = await call(
        'POST',
        '/attempts/$attemptId/actions',
        profileId: id,
        payload: {
          'request_id': ignoreId,
          'revision': before,
          'node': 'inspect',
          'choice': 'report',
        },
      );
      expect(conflict['status'], 409);
      await choose(
        'inspection_fix',
        'correct',
        '00000000-0000-4000-8000-000000000004',
      );
      expect((attempt['state'] as Map<String, dynamic>)['quality'], 63);

      final restored = await call('GET', '/attempts/$attemptId', profileId: id);
      expect(
        (restored['body'] as Map<String, dynamic>)['revision'],
        attempt['revision'],
      );
      expect(
        ((restored['body'] as Map<String, dynamic>)['state']
            as Map<String, dynamic>)['events'],
        hasLength(4),
      );
      final analytics = await call('GET', '/analytics', profileId: id);
      expect((analytics['body'] as Map<String, dynamic>)['errors'], 1);
    },
  );

  testWidgets('первая смена целиком завершается без сервера', (tester) async {
    final registered = await call(
      'POST',
      '/profiles',
      payload: {'name': 'Проверка полной смены'},
    );
    final id = (registered['body'] as Map<String, dynamic>)['id'] as String;
    var attempt =
        (await call('POST', '/attempts', profileId: id))['body']
            as Map<String, dynamic>;
    for (var step = 0; step < 40 && attempt['task'] != null; step++) {
      final task = attempt['task'] as Map<String, dynamic>;
      final selected =
          (task['choices'] as List<dynamic>).first as Map<String, dynamic>;
      final response = await call(
        'POST',
        '/attempts/${attempt['id']}/actions',
        profileId: id,
        payload: {
          'request_id':
              '00000000-0000-4000-8000-${(step + 100).toString().padLeft(12, '0')}',
          'revision': attempt['revision'],
          'node': (attempt['state'] as Map<String, dynamic>)['node'],
          'choice': selected['id'],
          'expedite': (task['wait'] as num? ?? 0) > 0,
        },
      );
      expect(response['ok'], true, reason: response['error']?.toString());
      attempt = response['body'] as Map<String, dynamic>;
    }
    expect(attempt['task'], isNull);
    expect((attempt['state'] as Map<String, dynamic>)['status'], 'complete');
    final profile = await call('GET', '/profile', profileId: id);
    expect((profile['body'] as Map<String, dynamic>)['completed'], 1);
    expect(
      (profile['body'] as Map<String, dynamic>)['achievements'],
      contains('Первая смена'),
    );
  });
}
