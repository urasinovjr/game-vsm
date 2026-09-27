import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

class ApiFailure implements Exception {
  const ApiFailure(this.message, {this.unauthorized = false});

  final String message;
  final bool unauthorized;

  @override
  String toString() => message;
}

class RegisteredProfile {
  const RegisteredProfile({required this.id, required this.token});

  final String id;
  final String token;
}

class TrainingProfile {
  const TrainingProfile({
    required this.id,
    required this.name,
    required this.completed,
    required this.achievements,
    required this.notifications,
    required this.competencies,
  });

  final String id;
  final String name;
  final int completed;
  final List<String> achievements;
  final List<String> notifications;
  final Map<String, int> competencies;

  factory TrainingProfile.fromJson(Map<String, dynamic> json) {
    final rawCompetencies = json['competencies'] as Map<String, dynamic>? ?? {};
    return TrainingProfile(
      id: json['id'] as String,
      name: json['name'] as String,
      completed: json['completed'] as int? ?? 0,
      achievements: (json['achievements'] as List<dynamic>? ?? [])
          .whereType<String>()
          .toList(),
      notifications: (json['notifications'] as List<dynamic>? ?? [])
          .whereType<String>()
          .toList(),
      competencies: rawCompetencies.map(
        (key, value) => MapEntry(key, (value as num).toInt()),
      ),
    );
  }
}

class LeaderboardEntry {
  const LeaderboardEntry({
    required this.name,
    required this.score,
    required this.self,
  });

  final String name;
  final int score;
  final bool self;

  factory LeaderboardEntry.fromJson(Map<String, dynamic> json) =>
      LeaderboardEntry(
        name: json['name'] as String,
        score: (json['score'] as num).toInt(),
        self: json['self'] as bool? ?? false,
      );
}

abstract class TrainingGateway {
  Future<RegisteredProfile> register(String name);
  Future<TrainingProfile> profile(String token);
  Future<List<LeaderboardEntry>> leaderboard(String token);
}

class HttpTrainingGateway implements TrainingGateway {
  HttpTrainingGateway({required String baseUrl, http.Client? client})
    : _baseUrl = baseUrl.replaceFirst(RegExp(r'/$'), ''),
      _client = client ?? http.Client();

  final String _baseUrl;
  final http.Client _client;

  Future<dynamic> _request(String path, {String? token, Object? body}) async {
    final uri = Uri.parse('$_baseUrl/api$path');
    final headers = <String, String>{'Accept': 'application/json'};
    if (token != null) headers['Authorization'] = 'Bearer $token';
    if (body != null) headers['Content-Type'] = 'application/json';

    try {
      final response =
          (body == null
                  ? _client.get(uri, headers: headers)
                  : _client.post(uri, headers: headers, body: jsonEncode(body)))
              .timeout(const Duration(seconds: 20));
      final result = await response;
      if (result.statusCode == 401) {
        throw const ApiFailure(
          'Учебный профиль больше не найден.',
          unauthorized: true,
        );
      }
      if (result.statusCode >= 400) {
        throw ApiFailure('Сервер не принял запрос (${result.statusCode}).');
      }
      return jsonDecode(utf8.decode(result.bodyBytes));
    } on ApiFailure {
      rethrow;
    } on TimeoutException {
      throw const ApiFailure('Сервер не ответил. Проверьте соединение.');
    } on http.ClientException {
      throw const ApiFailure('Нет связи с сервером обучения.');
    } on FormatException {
      throw const ApiFailure('Сервер вернул неожиданный ответ.');
    }
  }

  @override
  Future<RegisteredProfile> register(String name) async {
    final data = await _request('/profiles', body: {'name': name});
    return RegisteredProfile(
      id: (data as Map<String, dynamic>)['id'] as String,
      token: data['token'] as String,
    );
  }

  @override
  Future<TrainingProfile> profile(String token) async {
    final data = await _request('/profile', token: token);
    return TrainingProfile.fromJson(data as Map<String, dynamic>);
  }

  @override
  Future<List<LeaderboardEntry>> leaderboard(String token) async {
    final data = await _request('/leaderboard', token: token);
    return (data as List<dynamic>)
        .map((item) => LeaderboardEntry.fromJson(item as Map<String, dynamic>))
        .toList();
  }
}
