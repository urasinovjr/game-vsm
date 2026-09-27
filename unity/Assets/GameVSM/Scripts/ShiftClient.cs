using System;
using System.Collections;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace GameVSM
{
    // Android uses the app's local rules engine; standalone editor builds can use FastAPI.
    public sealed class ShiftClient : MonoBehaviour
    {
        public string BaseUrl = "http://127.0.0.1:8000";
        // Separate storage is useful for isolated development runs and training kiosks.
        public string SessionFileName = "shift-session.json";
        public JObject Attempt { get; private set; }
        public JObject Profile { get; private set; }
        public JObject Analytics { get; private set; }
        public JArray Leaderboard { get; private set; }
        public string Error { get; private set; } = "";
        public bool Busy { get; private set; }
        public event Action Changed;
        JObject session;
        string SavePath => Path.Combine(Application.persistentDataPath, SessionFileName);
        public bool CanResume => !string.IsNullOrEmpty((string)session?["attempt"]);
        public bool HasPending => session?["pending"] is JObject;
        public bool LaunchedFromHost { get; private set; }
        public bool LocalMode { get; private set; }
        public double ServerNow => Time.realtimeSinceStartupAsDouble + serverOffset;
        double serverOffset;

        void Awake()
        {
            session = new JObject();
            try { if (File.Exists(SavePath)) session = JObject.Parse(File.ReadAllText(SavePath)); }
            catch (Exception e) when (e is IOException || e is Newtonsoft.Json.JsonException)
            { Error = "Сохранённая смена не прочитана. Начните новую смену."; Debug.LogWarning("Файл сохранения повреждён: " + e); }
            if (!string.IsNullOrEmpty((string)session["baseUrl"])) BaseUrl = (string)session["baseUrl"];
            ReadHostSession();
        }

        void ReadHostSession()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                string profileId = intent.Call<string>("getStringExtra", "gamevsm_profile_id");
                if (string.IsNullOrEmpty(profileId)) return;
                if ((string)session["profileId"] != profileId || (bool?)session["localMode"] != true)
                    session = new JObject();
                session["profileId"] = profileId;
                session["localMode"] = true;
                LaunchedFromHost = true;
                LocalMode = true;
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("Не удалось прочитать данные приложения: " + e.Message);
            }
#endif
        }

        public void Connect(string url, bool resume)
        {
            if (Busy) return;
            if (LocalMode) { StartCoroutine(ConnectRoutine(resume)); return; }
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo))
            { Error = "Адрес сервера указан неверно. Откройте «Подключение» и введите адрес вида http://192.168.1.10:8000."; Changed?.Invoke(); return; }
            string next = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            // Never send an existing bearer token to a newly selected server.
            if ((string)session["baseUrl"] != next) session = new JObject { ["baseUrl"] = next };
            BaseUrl = next;
            StartCoroutine(ConnectRoutine(resume));
        }

        IEnumerator ConnectRoutine(bool resume)
        {
            Busy = true; Error = ""; Changed?.Invoke();
            if (!LocalMode && string.IsNullOrEmpty((string)session["token"]))
            {
                if (LaunchedFromHost)
                {
                    Error = "Профиль недоступен. Вернитесь в приложение и откройте смену снова.";
                    Busy = false; Changed?.Invoke();
                    yield break;
                }
                yield return Request("/profiles", new JObject { ["name"] = "Стажёр" }, result =>
                { session["token"] = result["token"]; Save(); });
            }
            if (string.IsNullOrEmpty(Error))
            {
                if (resume && CanResume)
                    yield return Request("/attempts/" + (string)session["attempt"], null, Apply);
                else if (HasPending)
                    Error = "Последнее действие ещё не подтверждено. Нажмите «Продолжить смену» и повторите действие.";
                else yield return Request("/attempts", new JObject(), Apply);
            }
            Busy = false; Changed?.Invoke();
        }

        public void Choose(string choice, bool expedite = false)
        {
            if (Busy || Attempt == null || HasPending) return;
            var world = GetComponent<ShiftExperience>();
            if (world != null && !world.CanChoose(out var reason))
            { Error = reason; Changed?.Invoke(); return; }
            Error = "";
            session["pending"] = new JObject {
                ["request_id"] = Guid.NewGuid().ToString(), ["revision"] = Attempt["revision"],
                ["node"] = Attempt["state"]?["node"], ["choice"] = choice, ["expedite"] = expedite
            };
            Save();
            StartCoroutine(Submit());
        }

        public void Retry() { if (!Busy && HasPending) StartCoroutine(Submit()); }

        public void LoadCareer() { if(!Busy && (LocalMode || session?["token"] != null)) StartCoroutine(Career()); }

        public void ReturnToApp()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("returnToApp");
                return;
            }
            catch (Exception error) { Debug.LogWarning("Не удалось вернуться в приложение: " + error.Message); }
#endif
            Application.Unload();
        }
        IEnumerator Career()
        {
            Busy=true;Error="";Changed?.Invoke();
            yield return Request("/profile",null,data=>Profile=data);
            if(Error.Length==0)yield return Request("/analytics",null,data=>Analytics=data);
            if(Error.Length==0)yield return RequestDocument("/leaderboard",null,data=>Leaderboard=data as JArray);
            Busy=false;Changed?.Invoke();
        }

        public void RefreshAttempt()
        {
            if (!Busy && Attempt != null && CanResume && !HasPending) StartCoroutine(RefreshRoutine());
        }

        IEnumerator RefreshRoutine()
        {
            Busy = true;
            yield return Request("/attempts/" + (string)session["attempt"], null, Apply);
            Busy = false; Changed?.Invoke();
        }

        IEnumerator Submit()
        {
            Busy = true; Error = ""; Changed?.Invoke();
            yield return Request("/attempts/" + (string)session["attempt"] + "/actions",
                (JObject)session["pending"], result => { session.Remove("pending"); Apply(result); });
            // A confirmed rejection cannot have awarded points. A lost response keeps
            // the exact UUID and payload so retrying cannot award points twice.
            if (lastStatus >= 400 && lastStatus < 500)
            {
                string rejection = Error;
                session.Remove("pending"); Save();
                if (CanResume) yield return Request("/attempts/" + (string)session["attempt"], null, Apply);
                Error = rejection;
            }
            Busy = false; Changed?.Invoke();
        }

        long lastStatus;
        IEnumerator Request(string path,JObject payload,Action<JObject> receive)
        {
            yield return RequestDocument(path,payload,data=>{
                if(data is JObject obj)receive(obj);else Unexpected(path,data?.ToString());
            });
        }
        IEnumerator RequestDocument(string path, JObject payload, Action<JToken> receive)
        {
            if (LocalMode)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var bridge = new AndroidJavaClass("ru.gamevsm.conductor.GameBridge");
                    string raw = bridge.CallStatic<string>("dispatch", activity,
                        payload == null ? "GET" : "POST", path, payload?.ToString() ?? "",
                        (string)session["profileId"] ?? "");
                    var envelope = JObject.Parse(raw);
                    lastStatus = (bool?)envelope["ok"] == true ? 200 : (long?)envelope["status"] ?? 500;
                    if (lastStatus == 200) receive(envelope["body"]);
                    else Error = (string)envelope["error"] ?? "Не удалось выполнить действие.";
                }
                catch (Exception error)
                {
                    lastStatus = 500;
                    Error = "Не удалось открыть локальные данные игры.";
                    Debug.LogException(error);
                }
#endif
                yield break;
            }
            using var request = new UnityWebRequest(BaseUrl + "/api" + path, payload == null ? "GET" : "POST");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 20;
            if (payload != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString()));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty((string)session["token"]))
                request.SetRequestHeader("Authorization", "Bearer " + (string)session["token"]);
            yield return request.SendWebRequest();
            lastStatus = request.responseCode;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Error = Describe(request, path);
                yield break;
            }
            JToken data;
            try { data = JToken.Parse(request.downloadHandler.text); }
            catch (Newtonsoft.Json.JsonException) { Unexpected(path, request.downloadHandler.text); yield break; }
            receive(data);
        }

        // Players get a cause and a next step; request details go to the diagnostic log only.
        string Describe(UnityWebRequest request, string path)
        {
            long code = request.responseCode;
            string detail = null;
            try { var d = JObject.Parse(request.downloadHandler.text)["detail"]; if (d?.Type == JTokenType.String) detail = (string)d; }
            catch (Exception e) when (e is Newtonsoft.Json.JsonException || e is ArgumentException) { }
            Debug.LogWarning($"Запрос {request.method} {path}: {request.result}, код {code}, {request.error}. Ответ: {request.downloadHandler.text}");
            // Server details are Russian player text; framework validation errors are not.
            if (detail != null && !System.Text.RegularExpressions.Regex.IsMatch(detail, "[А-Яа-яЁё]")) detail = null;
            if (code == 401 || (code == 404 && path.StartsWith("/attempts/")))
            {
                // The server no longer knows this profile/attempt, so a retry cannot succeed.
                if (code == 401) session.Remove("token");
                session.Remove("attempt"); session.Remove("pending"); Save();
                return "Эта смена не найдена на сервере (возможно, он был перезапущен). Нажмите «Начать смену».";
            }
            if (request.result == UnityWebRequest.Result.ConnectionError || code == 0)
                return "Нет связи с сервером. Проверьте сеть и адрес в разделе «Подключение», затем повторите.";
            if (code >= 500) return "Сервер смены временно недоступен. Прогресс не потерян: повторите через минуту.";
            if (code == 409) return "Состояние смены изменилось на сервере, например в другом окне. Загружено актуальное задание — продолжите с него.";
            if (code == 404) return "По этому адресу нет сервера смены. Проверьте адрес в разделе «Подключение».";
            return detail != null ? "Действие не принято: " + detail : "Действие не принято. Проверьте текущее задание и выберите действие снова.";
        }

        void Unexpected(string path, string body)
        {
            Debug.LogWarning($"Неожиданный ответ на {path}: {body}");
            Error = "Сервер ответил не так, как ожидает игра. Проверьте адрес в разделе «Подключение» или обновите сервер.";
        }

        void Apply(JObject value)
        {
            if (value["id"] == null || value["state"] == null || value["server_time"] == null)
            { Unexpected("смена", value.ToString()); return; }
            Attempt = value;
            serverOffset = (double)value["server_time"] - Time.realtimeSinceStartupAsDouble;
            session["attempt"] = value["id"];
            Save();
        }

        void OnApplicationPause(bool paused)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!LocalMode) return;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass("ru.gamevsm.conductor.GameBridge");
                bridge.CallStatic(paused ? "pause" : "resume", activity);
                if (!paused)
                {
                    string raw = bridge.CallStatic<string>("dispatch", activity, "CLOCK", "", "", "");
                    serverOffset = (double)JObject.Parse(raw)["body"]["server_time"] - Time.realtimeSinceStartupAsDouble;
                }
            }
            catch (Exception error) { Debug.LogWarning("Не удалось обновить игровой таймер: " + error.Message); }
#endif
        }

        void Save()
        {
            session["baseUrl"] = BaseUrl;
            string temporary = SavePath + ".tmp";
            var stored = (JObject)session.DeepClone();
            if (LaunchedFromHost) stored.Remove("token");
            File.WriteAllText(temporary, stored.ToString());
            File.Copy(temporary, SavePath, true);
            File.Delete(temporary);
        }
    }
}
