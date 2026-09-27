using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GameVSM
{
    public sealed class StudyHud : MonoBehaviour
    {
        public FirstPersonController Player;
        ShiftClient client;
        ShiftExperience experience;
        ShiftEnvironment environment;
        Font font;
        RectTransform canvas;
        GameObject menu;
        Text hint, objective, score, status, location, menuTitle, timer;
        Text direction, consequence, controls, menuLabel;
        readonly List<Button> actionChoices = new();
        GameObject connectButton, resumeButton, connectionSettings;
        InputField address;
        RectTransform choices;
        bool previousMenu;
        bool careerOpen;
        Button careerButton;
        double nextRefresh;
        readonly List<Button> timedChoices = new();
        readonly Color ink = new(.96f, .98f, 1f);
        readonly Color panel = new(.063f, .141f, .251f, .96f);
        readonly Color accent = new(.031f, .478f, 1f);
        Sprite rounded;

        void Start()
        {
            client = GetComponent<ShiftClient>();
            experience = GetComponent<ShiftExperience>();
            font = Resources.Load<Font>("Manrope");
            var root = new GameObject("Интерфейс", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            rounded = RoundedSprite();
            canvas = Area("Безопасная область", root.GetComponent<RectTransform>(),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            ApplySafeArea();
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            RectTransform header = Area("Header", canvas, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -86), Vector2.zero, panel);
            Label(header, "ПРОВОДНИК  /  ПЕРВАЯ СМЕНА", 24, new Vector2(30, -18), new Vector2(700, 35));
            location = Label(header, ShiftEnvironment.Title("Depot"), 17, new Vector2(30, -51), new Vector2(1000, 28));
            var menuButton = Button(header, InputHints.Menu, () => Player.SetMenu(!Player.MenuOpen));
            menuLabel = menuButton.GetComponentInChildren<Text>();
            Place(menuButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-245, -18), new Vector2(215, 48));
            var career=Button(header,"Профиль",()=>{careerOpen=true;Player.SetMenu(true);client.LoadCareer();Refresh();});
            careerButton=career;
            Place(career.GetComponent<RectTransform>(),new Vector2(1,1),new Vector2(-410,-18),new Vector2(145,48));
            objective = Label(canvas, "Начните смену в карточке задания", 22, new Vector2(30, -112), new Vector2(1100, 75));
            score = Label(canvas, "", 18, new Vector2(30, -185), new Vector2(1000, 35));
            timer = Label(canvas, "", 18, new Vector2(30, -219), new Vector2(1000, 35));
            direction = Label(canvas, "", 19, new Vector2(30, -265), new Vector2(1000, 50));
            consequence = Label(canvas, "", 20, new Vector2(30, -720), new Vector2(1050, 90));
            hint = Label(canvas, "", 22, Vector2.zero, new Vector2(1000, 64));
            Place(hint.rectTransform, new Vector2(.5f, 0), new Vector2(-500, 112), new Vector2(1000, 64));
            hint.alignment = TextAnchor.MiddleCenter;
            controls = Label(canvas, InputHints.Controls, 17, new Vector2(30, -850), new Vector2(1100, 30));
            var cross = Label(canvas, "+", 24, Vector2.zero, new Vector2(30, 30));
            Place(cross.rectTransform, new Vector2(.5f, .5f), new Vector2(-15, 15), new Vector2(30, 30));
            menu = Area("TaskPanel", canvas, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-390, -345), new Vector2(390, 345), panel).gameObject;
            var m = menu.GetComponent<RectTransform>();
            menuTitle = Label(m, "Первая смена", 34, new Vector2(30, -24), new Vector2(720, 50));
            Label(m, "Депо → Санкт-Петербург → рейс → Москва", 18, new Vector2(30, -79), new Vector2(720, 35));
            address = Input(m, client.BaseUrl, new Vector2(30, -410), new Vector2(720, 46));
            address.gameObject.SetActive(false);
            var settings=Button(m,"Подключение",()=>address.gameObject.SetActive(!address.gameObject.activeSelf));
            connectionSettings=settings.gameObject;
            Place(settings.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(30,-345),new Vector2(220,46));
            var connect = Button(m, "Начать смену", () => client.Connect(address.text, false));
            connectButton = connect.gameObject;
            Place(connect.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(30, -185), new Vector2(350, 48));
            var resume = Button(m, "Продолжить смену", () => client.Connect(address.text, true));
            resumeButton = resume.gameObject;
            Place(resume.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(400, -185), new Vector2(350, 48));
            if (client.LaunchedFromHost)
            {
                connectionSettings.SetActive(false);
                connectButton.SetActive(false);
                resumeButton.SetActive(false);
            }
            status = Label(m, StartText(),
                18, new Vector2(30, -252), new Vector2(720, 140));
            // Feedback + target + task + reason can exceed the card on a phone; shrink instead of cutting the last line.
            status.resizeTextForBestFit = true; status.resizeTextMinSize = 13; status.resizeTextMaxSize = 18;
            choices = Area("Choices", m, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(30, 86), new Vector2(-30, -395), Color.clear);
            var walk = Button(m, "Вернуться в игру", () => Player.SetMenu(false));
            Place(walk.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(30, 70), new Vector2(720, 48));
            if (client.LaunchedFromHost)
            {
                var returnButton = Button(m, "В приложение", client.ReturnToApp);
                Place(returnButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(30, 15), new Vector2(720, 46));
            }
            if (Application.isMobilePlatform)
            {
                Pad("Движение", new Vector2(0, 0), new Vector2(.25f, .35f), false);
                Pad("Осмотр", new Vector2(.65f, 0), new Vector2(1, .65f), true);
                var interact = Button(canvas, InputHints.ActionButton, () => Player.Interact());
                Place(interact.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(-260, 110), new Vector2(210, 60));
                menu.transform.SetAsLastSibling();
            }
            client.Changed += Refresh;
            if (experience != null) experience.Updated += Refresh;
            environment = GetComponent<ShiftEnvironment>();
            if (environment != null) environment.Changed += ChangeLocation;
            Player.SetMenu(true); previousMenu = true;
            if (client.LaunchedFromHost) client.Connect(client.BaseUrl, client.CanResume);
        }

        string StartText() =>
            "Примите состав в депо, помогите пассажирам и проведите рейс до Москвы. Подходите к людям и предметам: " +
            InputHints.Action("взаимодействовать") + ". Решения влияют на качество смены и доверие. Прогресс сохраняется на устройстве." +
            (client.CanResume ? "\n\n«Продолжить смену» вернёт сохранённую смену с того задания, на котором вы остановились." : "");

        void Pad(string name, Vector2 min, Vector2 max, bool look)
        {
            var pad = Area(name, canvas, min, max, new Vector2(20, 20), new Vector2(-20, -20), new Color(.2f, .35f, .4f, .12f));
            var input = pad.gameObject.AddComponent<TouchPad>(); input.Player = Player; input.Look = look;
            Label(pad, name, 20, new Vector2(20, -20), new Vector2(300, 40));
        }

        void Update()
        {
            if (menu == null) return;
            ApplySafeArea();
            if (previousMenu != Player.MenuOpen) { menu.SetActive(Player.MenuOpen); previousMenu = Player.MenuOpen; if(!Player.MenuOpen)careerOpen=false; }
            careerButton.interactable=client.Attempt!=null && !client.Busy;
            connectButton.GetComponent<Button>().interactable=!client.Busy;
            resumeButton.GetComponent<Button>().interactable=client.CanResume && !client.Busy;
            hint.text = Player.MenuOpen ? "" : Player.Hint;
            controls.text = InputHints.Controls; menuLabel.text = InputHints.Menu;
            if (experience != null)
            {
                float angle=Vector3.SignedAngle(Player.transform.forward,experience.TargetPosition-Player.transform.position,Vector3.up);
                string arrow=Mathf.Abs(angle)<25?"↑":Mathf.Abs(angle)>145?"↓":angle>0?"→":"←";
                direction.text = client.Attempt?["task"] is not JObject ? "" : arrow+" "+experience.TargetLabel + $" · {experience.Distance:F0} м";
                if (environment != null && environment.Transitioning) direction.text = environment.TransitionText;
                consequence.text = experience.Acting ? experience.ActionText : experience.Feedback;
                bool allowed = experience.CanChoose(out _);
                foreach (var button in actionChoices) if (button != null) button.interactable = !client.Busy && allowed;
            }
            var task = client.Attempt?["task"] as JObject;
            if (task == null) { timer.text = ""; return; }
            double remaining = Math.Max(0, ((double?)client.Attempt["state"]?["entered_at"] ?? 0) + ((double?)task["wait"] ?? 0) - client.ServerNow);
            foreach (var button in timedChoices) if (button != null) button.interactable = !client.Busy && remaining <= 0 && (experience == null || experience.CanChoose(out _));
            double? deadline = (double?)client.Attempt["state"]?["deadline"];
            timer.text = environment != null && environment.Transitioning ? "" : deadline.HasValue ? $"На организацию помощи: {Math.Max(0, (int)Math.Ceiling(deadline.Value - client.ServerNow))} с" :
                ((int?)task["wait"] ?? 0) > 0 ? "Можно продолжить обход или перейти дальше" : "";
            if (deadline.HasValue && deadline.Value <= client.ServerNow && Time.realtimeSinceStartupAsDouble >= nextRefresh)
            { nextRefresh = Time.realtimeSinceStartupAsDouble + 2; client.RefreshAttempt(); }
        }

        void ChangeLocation(string title) { location.text = title; }

        void Refresh()
        {
            foreach (Transform child in choices) Destroy(child.gameObject);
            timedChoices.Clear();
            actionChoices.Clear();
            if (!string.IsNullOrEmpty(client.Error)) status.text = client.Error;
            else status.text = client.Busy ? "Подождите…" : client.HasPending ? "Последнее действие ещё не подтверждено. Повторите его — оно не засчитается дважды." :
                client.Attempt != null ? "Прогресс сохранён." : StartText();
            if (client.HasPending)
            {
                AddChoice("Повторить действие", client.Retry, 0);
                return;
            }
            JObject attempt = client.Attempt;
            if (attempt == null)
            {
                if (client.LocalMode && !client.Busy)
                    AddChoice("Повторить открытие смены", () => client.Connect(client.BaseUrl, client.CanResume), 0);
                return;
            }
            // A lost server-side session leaves no attempt to continue, so starting anew must stay reachable.
            address.gameObject.SetActive(false); connectButton.SetActive(!client.CanResume); resumeButton.SetActive(false); connectionSettings.SetActive(!client.CanResume);
            if(careerOpen)
            {
                menuTitle.text="Профиль и результаты";
                var body=new System.Text.StringBuilder();
                if(client.Profile!=null)
                {
                    body.AppendLine($"{client.Profile["name"]} · завершённых смен: {client.Profile["completed"]}\n");
                    body.AppendLine("ДОСТИЖЕНИЯ");
                    foreach(var item in client.Profile["achievements"])body.AppendLine("• "+item);
                    body.AppendLine("\nУВЕДОМЛЕНИЯ");foreach(var item in client.Profile["notifications"])body.AppendLine("• "+item);
                    body.AppendLine("\nКОМПЕТЕНЦИИ");
                    foreach(var item in (JObject)client.Profile["competencies"])body.AppendLine((item.Key switch{"inspection"=>"Приёмка состава","communication"=>"Общение","safety"=>"Безопасность",_=>item.Key})+": "+item.Value);
                    if(!((JObject)client.Profile["competencies"]).HasValues)body.AppendLine("Завершите смену, чтобы получить результат.");
                    body.AppendLine($"\nРазбор: {client.Analytics?["decisions"]} решений · {client.Analytics?["errors"]} ошибок");
                    body.AppendLine("\nОБЩИЙ РЕЙТИНГ");
                    if (client.LocalMode) body.AppendLine("Появится после подключения синхронизации. Личный результат сохранён на устройстве.");
                    else if(client.Leaderboard!=null)foreach(var rank in client.Leaderboard)body.AppendLine($"{rank["name"]} — {rank["score"]}");
                }
                else body.AppendLine(client.Busy?"Загрузка…":client.Error);
                ShowDebrief(body.ToString());
                choices.offsetMax=new Vector2(-30,-530);
                AddChoice("Вернуться к смене",()=>{careerOpen=false;Refresh();Player.SetMenu(false);},0);
                return;
            }
            score.text = $"Качество: {attempt["state"]?["quality"]}     Доверие: {attempt["state"]?["trust"]}";
            var task = attempt["task"] as JObject;
            if (task == null)
            {
                bool stopped = (string)attempt["state"]?["status"] == "stopped";
                objective.text = stopped ? "Смена остановлена" : "Смена завершена";
                menuTitle.text = stopped ? "Разбор смены" : "Смена завершена";
                status.text = objective.text + ". Решения и результат сохранены.";
                var events = attempt["state"]?["events"] as JArray;
                var body = new System.Text.StringBuilder();
                if (events != null) foreach (var e in events)
                    body.AppendLine($"{e["title"]}\nВы выбрали: {e["label"]}\n{e["feedback"]}\nОснование: {e["source"]}\n");
                ShowDebrief(body.ToString());
                choices.offsetMax=new Vector2(-30,-530);
                AddChoice("Новая смена", () => client.Connect(client.BaseUrl,false), 0);
                return;
            }
            objective.text = (string)task["title"];
            var debrief=menu.transform.Find("Разбор");if(debrief!=null)Destroy(debrief.gameObject);
            status.gameObject.SetActive(true);
            choices.offsetMax=new Vector2(-30,-395);
            Place(status.rectTransform,new Vector2(0,1),new Vector2(30,-120),new Vector2(720,245));
            menuTitle.text = (string)task["phase"];
            if (!string.IsNullOrEmpty(client.Error)) return;
            status.text = (experience != null && experience.TargetIsPerson ? experience.TargetLabel + "\n\n" : "") + (string)task["text"];
            if (experience != null && experience.LastFeedback.Length > 0)
                status.text = "Итог прошлого шага: " + experience.LastFeedback + "\n\n" + status.text;
            if (experience != null && !experience.CanChoose(out var reason))
                status.text += "\n\n" + reason;
            int row = 0;
            foreach (JObject choice in task["choices"])
            {
                string id = (string)choice["id"];
                if (id == (string)task["timeout"]) continue;
                Button button = AddChoice((string)choice["label"], () => experience.Perform(id), row++);
                actionChoices.Add(button);
                if (((int?)task["wait"] ?? 0) > 0) timedChoices.Add(button);
            }
            if (((int?)task["wait"] ?? 0) > 0)
                actionChoices.Add(AddChoice("Перейти дальше", () => experience.Perform((string)task["choices"]?[0]?["id"], true), row));
        }

        void ShowDebrief(string body)
        {
            status.gameObject.SetActive(false);
            var old = menu.transform.Find("Разбор"); if (old != null) Destroy(old.gameObject);
            var area = Area("Разбор",menu.transform,new Vector2(0,0),new Vector2(1,1),new Vector2(28,200),new Vector2(-28,-120),panel);
            area.gameObject.AddComponent<RectMask2D>();
            var scroll = area.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.viewport = area;
            var text = Label(area,body,17,Vector2.zero,new Vector2(685,Mathf.Max(400,body.Length*.6f)));
            text.rectTransform.sizeDelta=new Vector2(685,Mathf.Max(370,text.preferredHeight+24));
            scroll.content = text.rectTransform; scroll.verticalNormalizedPosition = 1;
            var rail=Area("Прокрутка",area,new Vector2(1,0),Vector2.one,new Vector2(-12,3),new Vector2(0,-3),new Color(.15f,.22f,.26f));
            var handle=Area("Ползунок",rail,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,accent);
            var bar=rail.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=bar;
        }

        Button AddChoice(string text, Action action, int row)
        {
            var button = Button(choices, text, action);
            button.interactable = !client.Busy;
            Place(button.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, -row * 70), new Vector2(720, 60));
            return button;
        }

        RectTransform Area(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = color.a > 0;
            if (color.a > 0 && rounded != null) { image.sprite = rounded; image.type = Image.Type.Sliced; }
            return rect;
        }

        void ApplySafeArea()
        {
            if (canvas == null || Screen.width == 0 || Screen.height == 0) return;
            var safe = Screen.safeArea;
            canvas.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            canvas.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            canvas.offsetMin = canvas.offsetMax = Vector2.zero;
        }

        static Sprite RoundedSprite()
        {
            const int size = 32;
            const float radius = 8f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(8 - x, 0, x - 23);
                float dy = Mathf.Max(8 - y, 0, y - 23);
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100,
                0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
        }

        Text Label(Transform parent, string value, int size, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.color = ink;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(text.rectTransform, new Vector2(0, 1), position, dimensions); return text;
        }

        Button Button(Transform parent, string title, Action action)
        {
            var rect = Area(title, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, accent);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => action());
            Text label = Label(rect, title, 19, Vector2.zero, Vector2.zero);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12, 3); label.rectTransform.offsetMax = new Vector2(-12, -3);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        InputField Input(Transform parent, string value, Vector2 position, Vector2 size)
        {
            var rect = Area("Адрес сервера", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .19f, .23f));
            Place(rect, new Vector2(0, 1), position, size);
            Text text = Label(rect, "", 20, new Vector2(12, -8), size - new Vector2(24, 16));
            var input = rect.gameObject.AddComponent<InputField>(); input.textComponent = text; input.text = value;
            input.characterLimit = 180; return input;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; }

        void OnDestroy()
        {
            if (client != null) client.Changed -= Refresh;
            if (experience != null) experience.Updated -= Refresh;
            var environment = GetComponent<ShiftEnvironment>();
            if (environment != null) environment.Changed -= ChangeLocation;
        }
    }
}
