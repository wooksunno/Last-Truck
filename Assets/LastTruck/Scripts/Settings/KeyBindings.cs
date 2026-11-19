using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>설정 창 [조작키] 탭에서 바꿀 수 있는 행동 목록. 순서 = 설정 창에 보이는 순서.</summary>
public enum GameAction
{
    MoveForward,
    MoveBack,
    MoveLeft,
    MoveRight,
    Sprint,
    Jump,          // 점프 / 트럭 브레이크
    Interact,      // 상호작용 / 탑승 / 부활 / 함정 설치
    Inventory,     // 트럭 메뉴(인벤토리)
    Chat,
    TruckLight,
    DriverSeat,
    Turret,
    Reload,
    Quick1,
    Quick2,
    Quick3,
    Quick4,
    Attack,        // 마우스 왼쪽 고정 (설정 창에 나오지 않음)
}

/// <summary>
/// 모든 스크립트가 읽는 키 설정 한곳.
/// Input.GetKey(KeyCode.E) 대신 KeyBindings.Held(GameAction.Interact)처럼 쓰면 설정 창에서 바꾼 키를 그대로 따른다.
///  - 기본값 = 예전에 코드에 박혀 있던 키 (W/A/S/D, Shift, Space, E, Tab, Enter, L, F, T, R, 1~4, 마우스 왼쪽)
///  - 마우스 버튼(Mouse1~6)도 키로 쓸 수 있다. ESC / F3 / 마우스 왼쪽(공격)은 고정이라 고를 수 없다.
///  - 이미 다른 행동이 쓰는 키를 고르면 두 행동의 키를 서로 바꾼다 (설정 창이 처리).
///  - PlayerPrefs("LastTruck.Key.행동이름")에 저장한다.
/// 이동 축(Horizontal/Vertical)은 바뀐 이동 키로 계산하고, 방향키는 다른 행동에 쓰이지 않는 한 그대로 같이 동작한다.
/// </summary>
public static class KeyBindings
{
    #region 행동 정보

    public readonly struct ActionInfo
    {
        public readonly GameAction Action;
        public readonly string Label;
        public readonly KeyCode Default;
        public readonly bool Rebindable;

        public ActionInfo(GameAction action, string label, KeyCode def, bool rebindable = true)
        {
            Action = action;
            Label = label;
            Default = def;
            Rebindable = rebindable;
        }
    }

    private static readonly ActionInfo[] Infos =
    {
        new ActionInfo(GameAction.MoveForward, "이동 (앞)", KeyCode.W),
        new ActionInfo(GameAction.MoveBack, "이동 (뒤)", KeyCode.S),
        new ActionInfo(GameAction.MoveLeft, "이동 (왼쪽)", KeyCode.A),
        new ActionInfo(GameAction.MoveRight, "이동 (오른쪽)", KeyCode.D),
        new ActionInfo(GameAction.Sprint, "달리기", KeyCode.LeftShift),
        new ActionInfo(GameAction.Jump, "점프 / 트럭 브레이크", KeyCode.Space),
        new ActionInfo(GameAction.Interact, "상호작용 / 탑승", KeyCode.E),
        new ActionInfo(GameAction.Inventory, "인벤토리 (트럭 메뉴)", KeyCode.Tab),
        new ActionInfo(GameAction.Chat, "채팅", KeyCode.Return),
        new ActionInfo(GameAction.TruckLight, "트럭 라이트", KeyCode.L),
        new ActionInfo(GameAction.DriverSeat, "운전석으로 이동", KeyCode.F),
        new ActionInfo(GameAction.Turret, "포탑", KeyCode.T),
        new ActionInfo(GameAction.Reload, "재장전", KeyCode.R),
        new ActionInfo(GameAction.Quick1, "퀵슬롯 1", KeyCode.Alpha1),
        new ActionInfo(GameAction.Quick2, "퀵슬롯 2", KeyCode.Alpha2),
        new ActionInfo(GameAction.Quick3, "퀵슬롯 3", KeyCode.Alpha3),
        new ActionInfo(GameAction.Quick4, "퀵슬롯 4", KeyCode.Alpha4),
        new ActionInfo(GameAction.Attack, "공격", KeyCode.Mouse0, false),
    };

    /// <summary>설정 창에 보이는 행동들 (공격 제외).</summary>
    public static IEnumerable<ActionInfo> Rebindable
    {
        get
        {
            foreach (ActionInfo info in Infos)
                if (info.Rebindable) yield return info;
        }
    }

    public static ActionInfo InfoOf(GameAction action) => Infos[(int)action];

    /// <summary>어떤 행동에도 쓸 수 없는 키 (메뉴/디버그 고정 키 + 공격).</summary>
    public static bool IsReserved(KeyCode key)
    {
        return key == KeyCode.Escape || key == KeyCode.F3 || key == KeyCode.Mouse0 || key == KeyCode.None;
    }

    /// <summary>이 키를 행동 키로 쓸 수 있는가 (키보드 키 + Mouse1~6, 게임패드 버튼은 제외).</summary>
    public static bool IsBindable(KeyCode key)
    {
        if (IsReserved(key)) return false;
        if (key >= KeyCode.JoystickButton0) return false;
        return true;
    }

    #endregion

    #region 저장 / 불러오기

    private const string PrefPrefix = "LastTruck.Key.";
    private static KeyCode[] _keys;

    /// <summary>키가 바뀌었을 때 (설정 창 [적용] / 기본값 / 불러오기).</summary>
    public static event Action Changed;

    private static KeyCode[] Keys
    {
        get
        {
            if (_keys == null) Load();
            return _keys;
        }
    }

    public static void Load()
    {
        _keys = new KeyCode[Infos.Length];
        for (int i = 0; i < Infos.Length; i++)
        {
            KeyCode key = Infos[i].Default;
            if (Infos[i].Rebindable)
            {
                int saved = PlayerPrefs.GetInt(PrefPrefix + Infos[i].Action, (int)key);
                if (Enum.IsDefined(typeof(KeyCode), saved) && IsBindable((KeyCode)saved)) key = (KeyCode)saved;
            }
            _keys[i] = key;
        }
    }

    /// <summary>지금 키 전체를 복사해서 돌려준다 (설정 창이 편집용 사본으로 쓴다).</summary>
    public static KeyCode[] Snapshot() => (KeyCode[])Keys.Clone();

    public static KeyCode[] DefaultSnapshot()
    {
        var keys = new KeyCode[Infos.Length];
        for (int i = 0; i < Infos.Length; i++) keys[i] = Infos[i].Default;
        return keys;
    }

    /// <summary>편집한 사본을 적용 + 저장한다.</summary>
    public static void Apply(KeyCode[] keys)
    {
        if (keys == null || keys.Length != Infos.Length) return;
        _keys = (KeyCode[])keys.Clone();
        for (int i = 0; i < Infos.Length; i++)
        {
            if (!Infos[i].Rebindable) { _keys[i] = Infos[i].Default; continue; }
            PlayerPrefs.SetInt(PrefPrefix + Infos[i].Action, (int)_keys[i]);
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>
    /// 편집 사본에서 action에 key를 넣는다. 다른 행동이 이미 그 키를 쓰고 있으면 두 행동의 키를 서로 바꾸고 그 행동을 돌려준다.
    /// </summary>
    public static GameAction? Assign(KeyCode[] keys, GameAction action, KeyCode key)
    {
        int index = (int)action;
        KeyCode old = keys[index];
        GameAction? swapped = null;
        for (int i = 0; i < keys.Length; i++)
        {
            if (i == index || keys[i] != key || !Infos[i].Rebindable) continue;
            keys[i] = old;
            swapped = Infos[i].Action;
        }
        keys[index] = key;
        return swapped;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _keys = null;
        Changed = null;
        _smoothFrame = -1;
        _smoothH = 0f;
        _smoothV = 0f;
    }

    #endregion

    #region 입력 읽기

    public static KeyCode Get(GameAction action) => Keys[(int)action];

    /// <summary>누르고 있는 동안 true (Input.GetKey).</summary>
    public static bool Held(GameAction action) => Input.GetKey(Get(action));

    /// <summary>누른 순간 true (Input.GetKeyDown).</summary>
    public static bool Down(GameAction action) => Input.GetKeyDown(Get(action));

    /// <summary>뗀 순간 true (Input.GetKeyUp).</summary>
    public static bool Up(GameAction action) => Input.GetKeyUp(Get(action));

    /// <summary>퀵슬롯 번호(0~3)의 행동.</summary>
    public static GameAction QuickSlot(int index) => GameAction.Quick1 + Mathf.Clamp(index, 0, 3);

    /// <summary>Input.GetAxisRaw("Horizontal")를 대신한다: 오른쪽 +1, 왼쪽 -1.</summary>
    public static float Horizontal => AxisRaw(GameAction.MoveRight, GameAction.MoveLeft, KeyCode.RightArrow, KeyCode.LeftArrow);

    /// <summary>Input.GetAxisRaw("Vertical")를 대신한다: 앞 +1, 뒤 -1.</summary>
    public static float Vertical => AxisRaw(GameAction.MoveForward, GameAction.MoveBack, KeyCode.UpArrow, KeyCode.DownArrow);

    /// <summary>Input.GetAxis("Horizontal")처럼 부드럽게 따라가는 값 (트럭 가속 등).</summary>
    public static float HorizontalSmooth { get { UpdateSmooth(); return _smoothH; } }

    /// <summary>Input.GetAxis("Vertical")처럼 부드럽게 따라가는 값 (트럭 가속 등).</summary>
    public static float VerticalSmooth { get { UpdateSmooth(); return _smoothV; } }

    private static float AxisRaw(GameAction positive, GameAction negative, KeyCode altPositive, KeyCode altNegative)
    {
        float value = 0f;
        if (Held(positive) || AltHeld(altPositive)) value += 1f;
        if (Held(negative) || AltHeld(altNegative)) value -= 1f;
        return value;
    }

    /// <summary>방향키: 다른 행동에 쓰이지 않을 때만 이동 보조키로 같이 동작한다.</summary>
    private static bool AltHeld(KeyCode arrow)
    {
        KeyCode[] keys = Keys;
        for (int i = 0; i < keys.Length; i++)
            if (keys[i] == arrow) return false;
        return Input.GetKey(arrow);
    }

    // Unity 기본 Input Manager 축과 같은 값 (Gravity 3, Sensitivity 3, Snap 켜짐)
    private const float AxisSensitivity = 3f;
    private const float AxisGravity = 3f;
    private static int _smoothFrame = -1;
    private static float _smoothH, _smoothV;

    private static void UpdateSmooth()
    {
        if (_smoothFrame == Time.frameCount) return;
        _smoothFrame = Time.frameCount;
        float dt = Time.unscaledDeltaTime;
        _smoothH = Follow(_smoothH, Horizontal, dt);
        _smoothV = Follow(_smoothV, Vertical, dt);
    }

    private static float Follow(float current, float target, float dt)
    {
        if (Mathf.Approximately(target, 0f)) return Mathf.MoveTowards(current, 0f, AxisGravity * dt);
        if (Mathf.Sign(target) != Mathf.Sign(current) && !Mathf.Approximately(current, 0f)) current = 0f;   // Snap
        return Mathf.MoveTowards(current, target, AxisSensitivity * dt);
    }

    #endregion

    #region 표시 이름

    /// <summary>설정 창 / 안내 문구에 쓰는 키 이름 (예: "Left Shift", "마우스 오른쪽").</summary>
    public static string DisplayName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.None: return "-";
            case KeyCode.Mouse0: return "마우스 왼쪽";
            case KeyCode.Mouse1: return "마우스 오른쪽";
            case KeyCode.Mouse2: return "마우스 휠 클릭";
            case KeyCode.Mouse3: return "마우스 4";
            case KeyCode.Mouse4: return "마우스 5";
            case KeyCode.Mouse5: return "마우스 6";
            case KeyCode.Mouse6: return "마우스 7";
            case KeyCode.Return: return "Enter";
            case KeyCode.KeypadEnter: return "Num Enter";
            case KeyCode.Space: return "Space";
            case KeyCode.LeftShift: return "Left Shift";
            case KeyCode.RightShift: return "Right Shift";
            case KeyCode.LeftControl: return "Left Ctrl";
            case KeyCode.RightControl: return "Right Ctrl";
            case KeyCode.LeftAlt: return "Left Alt";
            case KeyCode.RightAlt: return "Right Alt";
            case KeyCode.BackQuote: return "`";
            case KeyCode.Minus: return "-";
            case KeyCode.Equals: return "=";
            case KeyCode.LeftBracket: return "[";
            case KeyCode.RightBracket: return "]";
            case KeyCode.Semicolon: return ";";
            case KeyCode.Quote: return "'";
            case KeyCode.Comma: return ",";
            case KeyCode.Period: return ".";
            case KeyCode.Slash: return "/";
            case KeyCode.Backslash: return "\\";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
        }
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "Num " + (int)(key - KeyCode.Keypad0);
        return key.ToString();
    }

    /// <summary>안내 문구용: "[E]" 같은 짧은 표시.</summary>
    public static string Hint(GameAction action) => "[" + DisplayName(Get(action)) + "]";

    #endregion
}
