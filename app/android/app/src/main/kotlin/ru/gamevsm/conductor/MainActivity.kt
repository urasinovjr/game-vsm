package ru.gamevsm.conductor

import android.content.Intent
import android.content.pm.ApplicationInfo
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import org.json.JSONObject

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "ru.gamevsm.conductor/training")
            .setMethodCallHandler { call, result ->
                val name = call.argument<String>("name") ?: ""
                val token = call.argument<String>("token") ?: ""
                val response = when (call.method) {
                    "register" -> GameBridge.dispatch(this, "POST", "/profiles",
                        JSONObject().put("name", name).toString(), "")
                    "profile" -> GameBridge.dispatch(this, "GET", "/profile", "", token)
                    "leaderboard" -> GameBridge.dispatch(this, "GET", "/leaderboard", "", token)
                    "debugEngine" -> {
                        if (applicationInfo.flags and ApplicationInfo.FLAG_DEBUGGABLE == 0) {
                            result.notImplemented()
                            return@setMethodCallHandler
                        }
                        GameBridge.dispatch(this,
                            call.argument<String>("httpMethod") ?: "GET",
                            call.argument<String>("path") ?: "",
                            call.argument<String>("payload") ?: "",
                            call.argument<String>("profileId") ?: "")
                    }
                    else -> { result.notImplemented(); return@setMethodCallHandler }
                }
                result.success(response)
            }
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "ru.gamevsm.conductor/game")
            .setMethodCallHandler { call, result ->
                if (call.method != "openGame") {
                    result.notImplemented()
                    return@setMethodCallHandler
                }
                val profileId = call.argument<String>("profileId")
                if (profileId.isNullOrBlank()) {
                    result.error("missing_session", "Нет данных учебного профиля", null)
                    return@setMethodCallHandler
                }
                try {
                    startActivity(Intent(this, GameActivity::class.java).apply {
                        putExtra("gamevsm_profile_id", profileId)
                    })
                    result.success(null)
                } catch (error: Exception) {
                    result.error("game_launch_failed", error.message, null)
                }
            }
    }
}
