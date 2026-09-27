# Unity-клиент

Редактор: Unity 6000.6.3f1, Universal Render Pipeline 17.6.0. Исходная сцена — `Assets/GameVSM/Scenes/Metallostroy.unity`. Папка `Assets/GameVSM` содержит игровой код, графику, окружение, сборочные команды и тесты; `Assets/Settings` — настройки URP.

Перед открытием после клонирования выполните `git lfs pull`. Модель поезда хранится в `Assets/GameVSM/Art/Train/WhiteKrechet.glb` через Git LFS. Не добавляйте в Git `Library`, `Temp`, `Builds`, логи или снимки регрессии.

`GameVSM.Editor.NativeBuild.Mac()` создаёт macOS development-сборку, `GameVSM.Editor.NativeBuild.Android()` — Android APK. Сервер должен быть доступен по адресу, заданному в игре. Android-сборка поддерживает ARM64 и Android API 26+; работа на физическом телефоне требует отдельной проверки.

`ShiftClient` получает состояние попытки и отправляет действия, `ShiftExperience` связывает серверные задачи со сценой. Начисление баллов и истечение таймеров выполняются сервером. Перед объединением с Flutter клиент должен получить адрес сервера и токен уже созданного профиля от Android-оболочки, без создания второго профиля и без хранения токена в открытом файле.
