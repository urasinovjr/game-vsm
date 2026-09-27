package ru.gamevsm.conductor

import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import android.os.SystemClock
import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

/** One local rules engine and database for the Flutter shell and the Unity activity. */
object GameBridge {
    private var activeSince: Long? = null
    private var activeBase = 0.0

    @JvmStatic
    @Synchronized
    fun resume(context: Context) {
        if (activeSince != null) return
        activeBase = context.getSharedPreferences("game_clock", Context.MODE_PRIVATE)
            .getFloat("seconds", 0f).toDouble()
        activeSince = SystemClock.elapsedRealtime()
    }

    @JvmStatic
    @Synchronized
    fun pause(context: Context) {
        activeBase = now()
        activeSince = null
        context.getSharedPreferences("game_clock", Context.MODE_PRIVATE)
            .edit().putFloat("seconds", activeBase.toFloat()).commit()
    }

    private fun now(): Double = activeBase + (activeSince?.let {
        (SystemClock.elapsedRealtime() - it) / 1000.0
    } ?: 0.0)

    @JvmStatic
    @Synchronized
    fun dispatch(context: Context, method: String, path: String, payload: String, profileId: String): String {
        return try {
            LocalGame(context.applicationContext).use { game ->
                val body = if (payload.isBlank()) JSONObject() else JSONObject(payload)
                val result: Any = when {
                    method == "CLOCK" -> JSONObject().put("server_time", now())
                    method == "POST" && path == "/profiles" -> game.register(body.getString("name"))
                    method == "GET" && path == "/profile" -> game.profile(profileId)
                    method == "GET" && path == "/analytics" -> game.analytics(profileId)
                    method == "GET" && path == "/leaderboard" -> JSONArray()
                    method == "POST" && path == "/attempts" -> game.start(profileId, now())
                    method == "GET" && path.startsWith("/attempts/") ->
                        game.attempt(profileId, path.substringAfterLast('/'), now())
                    method == "POST" && path.matches(Regex("/attempts/[^/]+/actions")) ->
                        game.action(profileId, path.split('/')[2], body, now())
                    else -> throw GameError(404, "Неизвестная команда игры.")
                }
                JSONObject().put("ok", true).put("body", result).toString()
            }
        } catch (error: GameError) {
            JSONObject().put("ok", false).put("status", error.status)
                .put("error", error.message).toString()
        } catch (error: Exception) {
            android.util.Log.e("GameBridge", "Local game failure", error)
            JSONObject().put("ok", false).put("status", 500)
                .put("error", "Не удалось прочитать сохранённую смену.").toString()
        }
    }
}

private class GameError(val status: Int, message: String) : IllegalStateException(message)

private class LocalGame(context: Context) : SQLiteOpenHelper(context, "gamevsm-local.db", null, 1) {
    private val graphText = context.assets.open("scenario.json").bufferedReader().use { it.readText() }

    override fun onCreate(db: SQLiteDatabase) {
        db.execSQL("CREATE TABLE profiles (id TEXT PRIMARY KEY, token TEXT UNIQUE NOT NULL, name TEXT NOT NULL)")
        db.execSQL("CREATE TABLE attempts (id TEXT PRIMARY KEY, profile_id TEXT NOT NULL, revision INTEGER NOT NULL, graph TEXT NOT NULL, state TEXT NOT NULL)")
        db.execSQL("CREATE INDEX attempt_owner ON attempts(profile_id)")
        db.execSQL("CREATE TABLE action_outbox (request_id TEXT PRIMARY KEY, attempt_id TEXT NOT NULL, profile_id TEXT NOT NULL, action TEXT NOT NULL, synced INTEGER NOT NULL DEFAULT 0)")
    }

    override fun onUpgrade(db: SQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit

    fun register(name: String): JSONObject {
        val trimmed = name.trim()
        if (trimmed.isEmpty() || trimmed.length > 40) throw GameError(422, "Укажите имя учебного профиля.")
        val id = UUID.randomUUID().toString()
        val token = UUID.randomUUID().toString()
        writableDatabase.execSQL("INSERT INTO profiles VALUES (?, ?, ?)", arrayOf(id, token, trimmed))
        return JSONObject().put("id", id).put("name", trimmed).put("token", token)
    }

    private fun owner(id: String): String = readableDatabase.rawQuery(
        "SELECT name FROM profiles WHERE id = ? OR token = ?", arrayOf(id, id)
    ).use { if (it.moveToFirst()) it.getString(0) else throw GameError(401, "Учебный профиль не найден.") }

    private fun canonicalId(id: String): String = readableDatabase.rawQuery(
        "SELECT id FROM profiles WHERE id = ? OR token = ?", arrayOf(id, id)
    ).use { if (it.moveToFirst()) it.getString(0) else throw GameError(401, "Учебный профиль не найден.") }

    private data class Attempt(val id: String, val revision: Int, val graph: JSONObject, val state: JSONObject)

    private fun load(profileId: String, id: String): Attempt = readableDatabase.rawQuery(
        "SELECT revision, graph, state FROM attempts WHERE id = ? AND profile_id = ?",
        arrayOf(id, profileId)
    ).use {
        if (!it.moveToFirst()) throw GameError(404, "Смена не найдена.")
        Attempt(id, it.getInt(0), JSONObject(it.getString(1)), JSONObject(it.getString(2)))
    }

    private fun save(attempt: Attempt) {
        writableDatabase.execSQL(
            "UPDATE attempts SET revision = ?, state = ? WHERE id = ?",
            arrayOf(attempt.revision, attempt.state.toString(), attempt.id)
        )
    }

    private fun node(attempt: Attempt): JSONObject = attempt.graph.getJSONObject("nodes")
        .getJSONObject(attempt.state.getString("node"))

    private fun next(attempt: Attempt, selected: JSONObject, at: Double): Attempt {
        val state = JSONObject(attempt.state.toString())
        val current = node(attempt)
        val beforeQuality = state.getInt("quality")
        val beforeTrust = state.getInt("trust")
        val quality = (beforeQuality + selected.optInt("quality")).coerceIn(0, 100)
        val trust = (beforeTrust + selected.optInt("trust")).coerceIn(0, 100)
        state.put("quality", quality).put("trust", trust)
        val competency = if (selected.isNull("competency")) "" else selected.optString("competency")
        if (competency.isNotEmpty()) {
            val scores = state.getJSONObject("competencies")
            scores.put(competency, scores.optInt(competency) +
                if (selected.optInt("quality") + selected.optInt("trust") > 0) 1 else -1)
        }
        val flag = if (selected.isNull("flag")) "" else selected.optString("flag")
        val flags = state.getJSONArray("flags")
        if (flag.isNotEmpty() && (0 until flags.length()).none { flags.getString(it) == flag }) flags.put(flag)
        state.getJSONArray("events").put(JSONObject()
            .put("id", UUID.randomUUID().toString())
            .put("node", current.getString("id"))
            .put("title", current.getString("title"))
            .put("choice", selected.getString("id"))
            .put("label", selected.getString("label"))
            .put("feedback", selected.getString("feedback"))
            .put("source", current.getString("source"))
            .put("quality", quality - beforeQuality)
            .put("trust", trust - beforeTrust)
            .put("at", at)
            .put("elapsed", at - state.getDouble("entered_at"))
            .put("serious", selected.optBoolean("serious")))
        val nextId = selected.getString("next")
        state.put("node", nextId).put("entered_at", at).put("deadline", JSONObject.NULL)
        if (nextId == "complete" || nextId == "stopped") {
            state.put("status", nextId).put("finished_at", at)
        } else {
            val seconds = attempt.graph.getJSONObject("nodes").getJSONObject(nextId).optInt("seconds")
            if (seconds > 0) state.put("deadline", at + seconds)
        }
        return attempt.copy(revision = attempt.revision + 1, state = state)
    }

    private fun reconcile(attempt: Attempt, at: Double): Attempt {
        if (attempt.state.getString("status") != "active" || attempt.state.isNull("deadline") ||
            at < attempt.state.getDouble("deadline")) return attempt
        val current = node(attempt)
        val timeout = current.getString("timeout")
        val choices = current.getJSONArray("choices")
        val selected = (0 until choices.length()).map { choices.getJSONObject(it) }
            .first { it.getString("id") == timeout }
        return next(attempt, selected, at).also(::save)
    }

    private fun view(attempt: Attempt, at: Double): JSONObject {
        val state = JSONObject(attempt.state.toString()).apply { remove("receipts") }
        val current = if (state.getString("status") == "active") node(attempt) else null
        val task = current?.let { JSONObject(it.toString()).apply {
            val timeout = optString("timeout")
            val choices = getJSONArray("choices")
            put("choices", JSONArray().apply {
                for (i in 0 until choices.length()) {
                    val choice = choices.getJSONObject(i)
                    if (choice.getString("id") != timeout) put(JSONObject()
                        .put("id", choice.getString("id"))
                        .put("label", choice.getString("label")))
                }
            })
            remove("timeout")
        } }
        return JSONObject().put("id", attempt.id).put("revision", attempt.revision)
            .put("state", state).put("task", task ?: JSONObject.NULL)
            .put("server_time", at)
            .put("training_status", "prototype-awaiting-expert-review")
    }

    fun start(profileId: String, at: Double): JSONObject {
        owner(profileId)
        val id = UUID.randomUUID().toString()
        val graph = JSONObject(graphText)
        val state = JSONObject()
            .put("node", graph.getString("start"))
            .put("version", graph.getString("version"))
            .put("status", "active")
            .put("quality", 70).put("trust", 70)
            .put("competencies", JSONObject().put("inspection", 0).put("communication", 0).put("safety", 0))
            .put("flags", JSONArray()).put("events", JSONArray()).put("receipts", JSONObject())
            .put("started_at", at).put("entered_at", at)
            .put("deadline", JSONObject.NULL).put("finished_at", JSONObject.NULL)
        writableDatabase.execSQL("INSERT INTO attempts VALUES (?, ?, 0, ?, ?)",
            arrayOf(id, profileId, graph.toString(), state.toString()))
        return view(Attempt(id, 0, graph, state), at)
    }

    fun attempt(profileId: String, id: String, at: Double): JSONObject {
        owner(profileId)
        return view(reconcile(load(profileId, id), at), at)
    }

    fun action(profileId: String, id: String, input: JSONObject, at: Double): JSONObject {
        owner(profileId)
        val db = writableDatabase
        db.beginTransaction()
        try {
            val attempt = reconcile(load(profileId, id), at)
            val requestId = input.getString("request_id")
            UUID.fromString(requestId)
            val fingerprint = "${input.getString("node")}:${input.getString("choice")}:${input.optBoolean("expedite")}"
            if (attempt.state.getJSONObject("receipts").has(requestId)) {
                if (attempt.state.getJSONObject("receipts").getString(requestId) != fingerprint)
                    throw GameError(409, "Это действие уже отправлено с другим выбором.")
                db.setTransactionSuccessful()
                return view(attempt, at)
            }
            if (attempt.state.getString("status") != "active") throw GameError(409, "Смена уже завершена.")
            if (attempt.revision != input.getInt("revision") || attempt.state.getString("node") != input.getString("node"))
                throw GameError(409, "Задание уже сменилось. Продолжите с текущего.")
            val current = node(attempt)
            val choiceId = input.getString("choice")
            if (choiceId == current.optString("timeout")) throw GameError(422, "Этот исход наступает только по истечении времени.")
            val choices = current.getJSONArray("choices")
            val selected = (0 until choices.length()).map { choices.getJSONObject(it) }
                .firstOrNull { it.getString("id") == choiceId }
                ?: throw GameError(422, "Такого действия нет в текущем задании.")
            val wait = current.optInt("wait")
            val expedite = input.optBoolean("expedite")
            if (expedite && wait == 0) throw GameError(422, "Здесь нельзя перейти дальше, не выполнив задание.")
            if (wait > 0 && !expedite && at < attempt.state.getDouble("entered_at") + wait)
                throw GameError(422, "Этот участок пути ещё не пройден. Можно перейти дальше.")
            val updated = next(attempt, selected, at)
            updated.state.getJSONObject("receipts").put(requestId, fingerprint)
            save(updated)
            db.execSQL("INSERT INTO action_outbox VALUES (?, ?, ?, ?, 0)",
                arrayOf(requestId, id, profileId, input.toString()))
            db.setTransactionSuccessful()
            return view(updated, at)
        } finally {
            db.endTransaction()
        }
    }

    fun profile(id: String): JSONObject {
        val name = owner(id)
        val actualId = canonicalId(id)
        val attempts = attempts(actualId)
        val completed = attempts.filter { it.state.getString("status") == "complete" }
        val best = completed.maxByOrNull { it.state.getInt("quality") + it.state.getInt("trust") }
        val achievements = JSONArray().apply {
            if (completed.isNotEmpty()) put("Первая смена")
            if (completed.any { a -> (0 until a.state.getJSONArray("events").length()).none { i ->
                    val event = a.state.getJSONArray("events").getJSONObject(i)
                    event.getInt("quality") < 0 || event.getInt("trust") < 0
                } }) put("Внимание к людям")
        }
        return JSONObject().put("id", actualId).put("name", name)
            .put("completed", completed.size).put("achievements", achievements)
            .put("competencies", best?.state?.getJSONObject("competencies") ?: JSONObject())
            .put("notifications", JSONArray().put(if (completed.isEmpty())
                "Первая смена готова к прохождению" else "Разбор новой смены доступен"))
    }

    fun analytics(id: String): JSONObject {
        owner(id)
        val attempts = attempts(canonicalId(id))
        var decisions = 0
        var errors = 0
        val versions = mutableSetOf<String>()
        val durations = mutableListOf<Double>()
        for (attempt in attempts) {
            val state = attempt.state
            versions.add(state.getString("version"))
            val events = state.getJSONArray("events")
            decisions += events.length()
            for (i in 0 until events.length()) {
                val event = events.getJSONObject(i)
                if (event.getInt("quality") < 0 || event.getInt("trust") < 0) errors++
            }
            if (!state.isNull("finished_at")) durations.add(state.getDouble("finished_at") - state.getDouble("started_at"))
        }
        return JSONObject().put("attempts", attempts.size)
            .put("completed", attempts.count { it.state.getString("status") == "complete" })
            .put("stopped", attempts.count { it.state.getString("status") == "stopped" })
            .put("decisions", decisions).put("errors", errors)
            .put("mean_duration_seconds", if (durations.isEmpty()) JSONObject.NULL else durations.average().toInt())
            .put("scenario_versions", JSONArray(versions.sorted()))
    }

    private fun attempts(id: String): List<Attempt> = readableDatabase.rawQuery(
        "SELECT id FROM attempts WHERE profile_id = ? ORDER BY rowid", arrayOf(id)
    ).use { cursor -> buildList {
        while (cursor.moveToNext()) add(load(id, cursor.getString(0)))
    } }
}
