# ARUCONANOPIANO
## Инструкция по работе и установке с приложением и репозиторием
## Оглавление
- [Установка приложения](#setup)
- [Установка репозитория в UnityProject](#installunity)
- [Запуск тестового примера в UnityProject](#testlaunch)
- [Подключение в приложении к серверу](#serverconnect)
- [Назначение аруко-меток на клавишах, руках и пианино](#arucoassign)
- [Проверка доступа к серверу в локальной сети](#checkaccess)
- [Пример работы](#workexample)
- [Документация по мосту Assets/UnityHTTPServer.cs](#bridgedoc)

<a id="setup"></a>
### Установка приложения
Установка приложения проходит в формате .apk, который был отправлен в тг-канале.
Приложение находится в release сборке. Название приложения camera_demo. После открытия .apk и тапа по согласию приложение должно появиться на рабочем столе.
<a id="installunity"></a>
### Установка репозитория в UnityProject
Версия проекта:
```
m_EditorVersion: 6000.5.0f1
m_EditorVersionWithRevision: 6000.5.0f1 (88b47c5e7076)
```
Установка через Git:
Так как репозиторий приватный, убедитесь, что вы сконфигурировали токен на вашем локальном компьютере и в вашем гитхаб-аккаунте для доступа:
[Официальная инструкция](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens).
Как только все готово, можете клонировать:
```bash
git clone https://github.com/Alexander-ha/ArucoNanoPiano-2026-06-19_03-18-33.git

```
Далее запускаем юнити-хаб на вашем ПК.
1. "Open" -> "Add Unity Project from disk"
2. Указать папку с репозиторием
3. double-click по проекту для открытия
4. Дождаться Unity-импорта проекта.

<a id="testlaunch"></a>
### Запуск тестового примера в UnityProject
> 📌 **Примечание:** Эти процессы не обязательны, если уже и так понятно что делать: можно сразу перейти к [Документация по мосту Assets/UnityHTTPServer.cs](#bridgedoc),
> чтобы использовать функции http-доступа, а также к  [Подключение в приложении к серверу](#serverconnect) и [Пример работы](#workexample).


Если установка прошла успешно, то вы увидите следующее в поле сцены SampleScene:
![изобр. 1, сцена](arcuopianoinstr/inspectorinstr0.png)

Здесь можно увидеть, что в дереве Canvas есть Panel, а потом наследуется несколько объектов типа Text. 
В примере, который мы будем юзать - сделано все просто, на панель насажено 5 текстовых полей.

В правой части в инспекторе можно видеть следующее:
![изобр. 1, инспектор](arcuopianoinstr/inspectorinstr1.png)


Это основной скрипт Assets/UnityHTTPServer ([ссылка на него в репозитории](Assets/UnityHTTPServer.cs).
Он добавлен как Component к MainCamera.
Здесь по списку:
1. Поля для текста (на сцене в них буду заноситься значения, парсящиеся по http-запросу), сами объекты полей закидываем из левой панели сцены (те что под Canvas, если они не закидываются).
```cs
    public Text statusText;
    public Text leftHandText;
    public Text rightHandText;
    public Text pianoText;
    public Text extraKeysText;
```
2. Порт. Обязательно укажите именно свободный порт.

Как только все будет сконфигурировано, можно жать ▶, сервер заработает и будет слушать порт, указанный вами (по умолчанию 8081).


<a id="serverconnect"></a>
### Подключение в приложении к серверу
Теперь, когда сервер запущен, вызовите в cmd вашего ПК следующее:
```bash
ipconfig

>
Адаптер беспроводной локальной сети Беспроводная сеть:

   DNS-суффикс подключения . . . . . : Dlink
   Локальный IPv6-адрес канала . . . : fe80::d55e:1f9e:3655:ebc4%9
   IPv4-адрес. . . . . . . . . . . . : 192.168.0.138
   Маска подсети . . . . . . . . . . : 255.255.255.0
   Основной шлюз. . . . . . . . . : 192.168.0.1

Адаптер Ethernet outline-tap0:

   Состояние среды. . . . . . . . : Среда передачи недоступна.
   DNS-суффикс подключения . . . . . :

Адаптер Ethernet Сетевое подключение Bluetooth:

   Состояние среды. . . . . . . . : Среда передачи недоступна.
   DNS-суффикс подключения . . . . . :
```

Из того, что вы получите, вам будет нужен именно IPv4-адрес. 

Перейдите в приложение camera_demo.apk, **которое вы скачали на ваш телефон**.

![изобр 3. меню](arcuopianoinstr/Screenshot_2026-06-21-23-10-56-646_com.example.flora_nano_aruco.jpg)

В приложении нажмите Connect to Local Server.

![изобр 4. коннект](arcuopianoinstr/Screenshot_2026-06-21-22-40-37-466_com.example.flora_nano_aruco.jpg)

Введите ваш IPv4 адресс ПК-сервера на юнитии в **локальной wi-fi сети**, а также порт, который указали при запуске скрипта.

Если все сделано верно - вы увидите зеленую надпись с галочкой, где будет указано, что вы подключились.

> [!WARNING]
> Типичные проблемы которые могут привести к неудачному подключению:
> 1. **Ваш ПК, на котором работает unity-server и ваш телефон находятся НЕ в одной  Wi-Fi сети**. Решение: подключиться к одному Wi-Fi.
> 2. **На вашем ПК работает Брандмауэр.** Решение: ufw ubuntu отключается через sudo ufw disable, windows: netsh advfirewall set allprofiles state off.
> 3. **Вы отключили брандмауэр через терминал, но сеть индентифицируется не как домашняя, а как общая** Решение: на Windows можно зайти Панель управления\Система и безопасность\Брандмауэр Защитника Windows и поотключать к хуям все брандмауэры.
> 4. **Ваша версия андроид по какой-то причине не поддерживает текущие сертификаты** Решение: написать мне @altergan1, что нибудь придумаем.





<a id="arucoassign"></a>
### Назначение аруко-меток на клавишах, руках и пианино
Как только вы подключитесь, перед вами сразу же откроется экран выбора клавиш. Если нет: пикните в главном меню Enter ArUco ID.

![изобр 5. клавиши](arcuopianoinstr/Screenshot_2026-06-22-00-52-01-479_com.example.flora_nano_aruco.jpg)

Здесь вы можете:
1. указать аруко-метку на левой руке (Left Hand ID)
2. указать аруко-метку на правой руке (Right Hand ID)
3. указать аруко-метку на пианино (Piano ID)
4. через add key + добавить неограниченное количество меток на клавиши, каждой присвоив id, который обозначает ваша конкретная аруко-метка.
<a id="checkaccess"></a>
### Проверка доступа к серверу в локальной сети

Хорошей вариантом при отсутсвии подключения является проверить, имеется ли доступ к вашему ПК-серверу unity в целом: 
для этого можно вбить ip и порт прямо в браузер: http://192.168.0.138:8081/
И вы увидите, есть ли у вас доступ.
Если нет - вернитесь к разделу Warning в [Подключение в приложении к серверу](#serverconnect). 

<a id="workexample"></a>
### Пример работы
Если все сконфигурировано правильно вы можете нажать Open Camera в вашем приложении и разрешить к ней доступ.
После чего вы сможете тыкать метки (пример на фото):

![тык1](arcuopianoinstr/Screenshot_2026-06-21-23-39-36-447_com.example.flora_nano_aruco.jpg)

![тык2](arcuopianoinstr/Screenshot_2026-06-22-00-52-54-088_com.example.flora_nano_aruco.jpg)

На сервере же вы увидите следующее:
![серв](arcuopianoinstr/instructionarucopiano.png)

> [!NOTE]
> 1. **Метки отправляются на сервер только в формате x,y координат границ бокса. Если нужна координата z-используйте trick c вычислением глубины на самом сервере(код придется писать отдельно).**
> 2. **Если метку не видно - на сервер приходит null**
> 3. **Кортеж extraKeys имеет произвольный размер в зависимости от того, сколько клавиш вы назначили в приложении на Android**

Пример передаваемого json:

```json
1. Видим все маркеры:
json
{
  "left_hand": {
    "id": 1,
    "corners": [{"x": 100, "y": 200}, {"x": 150, "y": 205}, {"x": 155, "y": 260}, {"x": 105, "y": 255}]
  },
  "right_hand": {
    "id": 2,
    "corners": [{"x": 300, "y": 200}, ...]
  },
  "piano": {
    "id": 3,
    "corners": [{"x": 500, "y": 400}, ...]
  },
  "extra_keys": {
    "key_4": {
      "id": 4,
      "corners": [{"x": 200, "y": 300}, ...]
    }
  }
}
2. Не видим left_hand и key_4 (но остальные видны):
json
{
  "left_hand": null,
  "right_hand": {
    "id": 2,
    "corners": [{"x": 300, "y": 200}, ...]
  },
  "piano": {
    "id": 3,
    "corners": [{"x": 500, "y": 400}, ...]
  },
  "extra_keys": {
    "key_4": null
  }
}
```


<a id="bridgedoc"></a>
### Документация по мосту Assets/UnityHTTPServer.cs
Основной скрипт, позволяющий конфигурировать доступ по http валяется в assets, это UnityHTTPServer.cs. Он дает стандартный доступ к GET-запросам, наследуется от MonoBehaviour.
## 📋 Публичные методы

### `StartHTTPServer()`
Запускает http-сервер на указанном порту.

```csharp
public void StartHTTPServer()
```

**Пример:**
```csharp
UnityHTTPServer server = GetComponent<UnityHTTPServer>();
server.StartHTTPServer();
```

---

### `ProcessReceivedData(string jsonData)`
обрабатывает получаемый json, обновляет юзео-интерфейс (через переданные текстовые поля).

```csharp
public void ProcessReceivedData(string jsonData)
```

| Параметр | Тип | Описание |
|----------|-----|----------|
| `jsonData` | `string` | JSON-строка с данными жестов |

**Пример:**
```csharp
server.ProcessReceivedData("{\"left_hand\":{\"corners\":[{\"x\":100,\"y\":200}]}}");
```

---

### `UpdateUI(RootObject data)`
Обновляет все UI-элементы на основе полученных данных.

```csharp
public void UpdateUI(RootObject data)
```

| Параметр | Тип | Описание |
|----------|-----|----------|
| `data` | `RootObject` | Десериализованный объект с данными |

---

### `CalculateCenter(List<Corner> corners)`
Вычисляет центр объекта по списку угловых точек. (может сгодиться для позиционирования.)

```csharp
public Vector2 CalculateCenter(List<Corner> corners)
```

| Параметр | Тип | Описание |
|----------|-----|----------|
| `corners` | `List<Corner>` | Список точек (x, y) |

**Возвращает:** `Vector2` — центр объекта.

**Пример:**
```csharp
Vector2 center = CalculateCenter(handData.corners);
Debug.Log($"Центр: ({center.x}, {center.y})");
```

---

### `GetLocalIPAddress()`
Получает локальный IP-адрес компьютера (авто-конфиг айпишника).

```csharp
public string GetLocalIPAddress()
```

**Возвращает:** `string` — IP-адрес (например, `192.168.1.100`).

**Пример:**
```csharp
string ip = GetLocalIPAddress();
statusText.text = $"Сервер: http://{ip}:8080/";
```

---

## 🔒 Приватные методы

| Метод | Описание |
|-------|----------|
| `HandleRequests()` | корутина для асинхронной обработки запросов |
| `ProcessRequest(IAsyncResult result)` | обработка на входящий HTTP-запрос |
| `Update()` | апдейт каждый кадр |

---

## 📦 вложенные классы

### `Corner`
точка.

```csharp
public class Corner
{
    public float x;
    public float y;
}
```

### `HandData`
рука.

```csharp
public class HandData
{
    public int id;
    public List<Corner> corners;
}
```

### `PianoData`
пиаинино.

```csharp
public class PianoData
{
    public int id;
    public List<Corner> corners;
}
```

### `KeyData`
клавиша.

```csharp
public class KeyData
{
    public int id;
    public List<Corner> corners;
}
```

### `ExtraKeys`
контейнер для клавиш.

```csharp
public class ExtraKeys
{
    public KeyData key_1;
    public KeyData key_2;
    // ... до key_10
}
```

### `RootObject`
корень JSON.

```csharp
public class RootObject
{
    public HandData left_hand;
    public HandData right_hand;
    public PianoData piano;
    public ExtraKeys extra_keys;
}
```

---

## 🎯 Пример использования

```csharp
// получение сервера
UnityHTTPServer server = GetComponent<UnityHTTPServer>();

// запуск его с текущими параметрами
server.StartHTTPServer();

// геттер на апйишник (можем получить )
string ip = server.GetLocalIPAddress();
Debug.Log($"Сервер запущен на {ip}:8080");

// ручная десериализация
string json = "{\"left_hand\":{\"corners\":[{\"x\":100,\"y\":200}]}}";
server.ProcessReceivedData(json);

// барицентр
List<Corner> corners = new List<Corner>();
corners.Add(new Corner { x = 0, y = 0 });
corners.Add(new Corner { x = 100, y = 100 });
Vector2 center = server.CalculateCenter(corners);
Debug.Log($"Центр: {center}");
```

---

## ⚡ Unity-события
дефолтные события
| Событие | Когда вызывается |
|---------|------------------|
| `Start()` | запустилась сцена - запустился сервер |
| `Update()` | Каждый кадр — обрабатывает полученные данные |
| `OnDestroy()` | стоп - уничтожение объекта сервера из памяти|


