import 'dart:convert';

import 'package:flutter/services.dart';

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

class LocalTrainingGateway implements TrainingGateway {
  static const _channel = MethodChannel('ru.gamevsm.conductor/training');

  Future<dynamic> _call(String method, Map<String, String> args) async {
    try {
      final raw = await _channel.invokeMethod<String>(method, args);
      final response = jsonDecode(raw ?? '') as Map<String, dynamic>;
      if (response['ok'] != true) {
        throw ApiFailure(
          response['error'] as String? ??
              'Не удалось открыть локальные данные.',
          unauthorized: response['status'] == 401,
        );
      }
      return response['body'];
    } on PlatformException {
      throw const ApiFailure('Не удалось открыть локальные данные игры.');
    } on FormatException {
      throw const ApiFailure('Локальные данные игры повреждены.');
    }
  }

  @override
  Future<RegisteredProfile> register(String name) async {
    final data =
        await _call('register', {'name': name}) as Map<String, dynamic>;
    return RegisteredProfile(
      id: data['id'] as String,
      token: data['token'] as String,
    );
  }

  @override
  Future<TrainingProfile> profile(String token) async {
    final data =
        await _call('profile', {'token': token}) as Map<String, dynamic>;
    return TrainingProfile.fromJson(data);
  }

  @override
  Future<List<LeaderboardEntry>> leaderboard(String token) async {
    final data = await _call('leaderboard', {'token': token}) as List<dynamic>;
    return data
        .map(
          (entry) => LeaderboardEntry.fromJson(entry as Map<String, dynamic>),
        )
        .toList();
  }
}
