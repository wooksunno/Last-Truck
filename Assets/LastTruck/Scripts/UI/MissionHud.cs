using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 임무 UI (UI Toolkit, 움직임은 코드로 직접 그린다).
    ///
    ///  [임무 카드] 화면 왼쪽: 일차 진행 칸 + 날씨 + "오늘 밤 생존 조건"(낮에는 예고 / 밤에는 진행 막대).
    ///     - 생존 조건: 좀비 처치 수 / 버틴 시간 / 중간보스·웨이브 수 막대, 트럭 누적 피해 게이지(20% 한도), 다음 웨이브·보스까지 시간 막대.
    ///     - 탈출 작전(부품 체크리스트 + 게이트 방향)은 모든 생존 조건을 돌파한 뒤에야 카드 아래에 나타난다.
    ///     - 처음 나올 때 옆에서 튀어 들어오고, 낮/밤이 바뀌면 조건 글이 갈아 끼워지며 번쩍인다. 달성하면 초록, 실패하면 빨강으로 번쩍인다.
    ///  [무전 대화창] 화면 아래 가운데: 무전/일지 대사가 신호 잡음처럼 떨며 나타나고, 이퀄라이저가 움직이며 글자가 한 자씩 찍힌다.
    ///     다 읽히면 잠시 머물다 가라앉으며 사라진다.
    /// </summary>
    public class MissionHud
    {
        private static readonly Color Gold = new Color(1f, 0.8f, 0.34f, 1f);
        private static readonly Color Night = new Color(0.74f, 0.6f, 1f, 1f);
        private static readonly Color Green = new Color(0.4f, 1f, 0.62f, 1f);
        private static readonly Color Red = new Color(1f, 0.4f, 0.36f, 1f);
        private static readonly Color Cream = new Color(0.97f, 0.93f, 0.82f, 1f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.16f);

        private readonly StyleFontDefinition _font;
        private readonly Sprite _cardSkin, _radioSkin;

        // ---- 카드
        private VisualElement _card, _accent, _flash, _objBox, _survBox, _escBox, _partsBox, _gateRow, _arrow;
        private Label _title, _dayLabel, _kindTag, _condTitle, _condDetail, _goalLabel, _wxLabel, _wxHint, _wxPenalty;
        private VisualElement _wxDot;
        private VisualElement[] _pips;
        private VisualElement[] _partRow, _partCheck;
        private bool[] _owned;
        private float[] _pop;
        private float _cardIntro;
        private bool _cardOpen;               // 카드가 열려 있는가
        private bool _cardWasOpen;            // 지난 프레임 (열고 닫는 소리용)
        private bool _pinned;                 // K 키로 열어 둔 상태
        private float _peekUntil;             // 낮/밤이 바뀔 때 이 시각까지 자동으로 열려 있다
        private float _objFx = 1f, _flashFx = 1f;
        private float _nextPartCheck;
        private string _objectiveText = "";
        private bool _hasGate;
        private SurvivalResult _lastResult = SurvivalResult.Active;

        // 생존 조건 막대
        private class Bar { public VisualElement root, fill; public Label text; public float shown; }
        private Bar _prog, _dmg, _next;
        private VisualElement _dmgRow, _nextRow, _bossRow, _bossCheck;
        private Label _dmgLabel, _nextLabel, _bossLabel;
        private float _bossPop = 1f;
        private bool _bossWasDone;

        // ---- 무전
        private enum RadioState { Hidden, Intro, Typing, Hold, Outro }
        private VisualElement _radio, _dot, _radioIcon;
        private VisualElement[] _eq;
        private Label _speaker, _status, _body;
        private RadioState _rs = RadioState.Hidden;
        private string _full = "";
        private float _rt, _typed, _holdFor;
        private bool _journal;

        // 카드 자리: 왼쪽 아래 구석
        private const float CardScale = 0.84f, CardWidth = 470f, CardBottom = 26f;

        public MissionHud(StyleFontDefinition font, Sprite cardSkin, Sprite radioSkin)
        {
            _font = font;
            _cardSkin = cardSkin;
            _radioSkin = radioSkin;
        }

        // ================================================================ 만들기

        public void Build(VisualElement root)
        {
            BuildCard(root);
            BuildRadio(root);
        }

        private void BuildCard(VisualElement root)
        {
            _hasGate = EscapeGate.Instance != null;

            _card = new VisualElement { pickingMode = PickingMode.Ignore };
            _card.style.position = Position.Absolute;
            // 왼쪽 아래 구석에 둔다 (화면 가운데를 가리지 않는다).
            // 예전 크기 그대로 0.84배로 줄여서 그린다: 글자/막대 비율은 유지하고 도전과제 카드와 글자 크기가 비슷해진다.
            LastTruckUiSkin skin = LastTruckUiSkin.Get();
            bool poly = skin != null && skin.polyTheme && skin.hudFade != null;
            _card.style.left = 24;
            _card.style.width = CardWidth / CardScale;
            _card.style.scale = new Scale(new Vector3(CardScale, CardScale, 1f));
            _card.style.paddingLeft = 26; _card.style.paddingRight = 20;
            _card.style.paddingTop = 18; _card.style.paddingBottom = 18;
            if (poly)
            {
                // 원신풍 임무 추적기: 왼쪽 위(트럭 내구도 막대 아래), 판 없이 글자 + 왼쪽에서 사라지는 그늘
                _card.style.top = 64;
                _card.style.transformOrigin = new TransformOrigin(new Length(0f, LengthUnit.Percent), new Length(0f, LengthUnit.Percent));
                LastTruckHud.Skin(_card, skin.hudFade, 1f, Color.white);
            }
            else
            {
                _card.style.bottom = CardBottom;
                _card.style.transformOrigin = new TransformOrigin(new Length(0f, LengthUnit.Percent), new Length(100f, LengthUnit.Percent));
                LastTruckHud.Skin(_card, _cardSkin, 0.5f, new Color(0.7f, 0.6f, 0.55f, 0.97f));
                if (_cardSkin == null) _card.style.backgroundColor = new Color(0.1f, 0.08f, 0.07f, 0.92f);
            }
            _card.style.opacity = 0f;

            // 왼쪽 띠 (낮=금색, 밤=보라)
            _accent = new VisualElement { pickingMode = PickingMode.Ignore };
            _accent.style.position = Position.Absolute;
            _accent.style.left = 10; _accent.style.top = 16; _accent.style.bottom = 16; _accent.style.width = poly ? 3 : 6;
            Radius(_accent, poly ? 1.5f : 3f);
            _accent.style.backgroundColor = Gold;
            _card.Add(_accent);

            // 임무 제목과 단축키
            var head = Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.marginBottom = 6;
            var left = Row();
            VisualElement flag = Icon("flag", 26f);
            if (flag != null)
            {
                flag.style.marginRight = 8;
                flag.style.unityBackgroundImageTintColor = Gold;
                left.Add(flag);
            }
            _title = Text("생존 임무", 30, Cream);
            left.Add(_title);
            head.Add(left);
            head.Add(KeyCap("J"));
            _card.Add(head);

            // 일차 + 진행 칸 (생존해야 하는 날 수)
            var days = Row();
            days.style.marginBottom = 8;
            _dayLabel = Text("1일차", 20, new Color(1f, 1f, 1f, 0.7f));
            _dayLabel.style.marginRight = 10;
            days.Add(_dayLabel);
            int count = _hasGate ? StoryDirector.EscapeDay - 1 : 0;
            _pips = new VisualElement[count];
            for (int i = 0; i < count; i++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.style.width = 24; pip.style.height = 9; pip.style.marginRight = 4;
                Radius(pip, 4.5f);
                pip.style.backgroundColor = Dim;
                days.Add(pip);
                _pips[i] = pip;
            }
            _card.Add(days);

            // 날씨
            var wx = Row();
            wx.style.marginBottom = 2;
            _wxDot = new VisualElement { pickingMode = PickingMode.Ignore };
            _wxDot.style.width = 12; _wxDot.style.height = 12; _wxDot.style.marginRight = 8;
            Radius(_wxDot, 6);
            wx.Add(_wxDot);
            _wxLabel = Text("날씨  맑음", 22, Cream);
            wx.Add(_wxLabel);
            _card.Add(wx);
            _wxHint = Text("", 19, new Color(1f, 1f, 1f, 0.6f));
            _wxHint.style.display = DisplayStyle.None;
            _card.Add(_wxHint);
            _wxPenalty = Text("", 18, new Color(1f, 0.55f, 0.45f, 1f));
            _wxPenalty.style.marginBottom = 8;
            _wxPenalty.style.display = DisplayStyle.None;
            _card.Add(_wxPenalty);

            _card.Add(Divider());

            // ---------------- 생존 조건
            _survBox = new VisualElement { pickingMode = PickingMode.Ignore };
            _survBox.style.marginTop = 10;

            _objBox = new VisualElement { pickingMode = PickingMode.Ignore };
            _kindTag = Text("오늘 밤 예고", 20, new Color(1f, 1f, 1f, 0.6f));
            _kindTag.style.marginBottom = 2;
            _objBox.Add(_kindTag);
            _condTitle = Text("", 28, Gold);
            _condTitle.style.marginBottom = 4;
            _objBox.Add(_condTitle);
            _condDetail = Text("", 20, new Color(1f, 1f, 1f, 0.72f));
            _condDetail.style.marginBottom = 10;
            _objBox.Add(_condDetail);
            _survBox.Add(_objBox);

            _prog = MakeBar(28, 19);
            _survBox.Add(_prog.root);

            // 트럭 누적 피해 (트럭 방어 밤)
            _dmgRow = new VisualElement { pickingMode = PickingMode.Ignore };
            _dmgRow.style.marginTop = 10;
            _dmgLabel = Text("", 18, Cream);
            _dmgLabel.style.marginBottom = 3;
            _dmgRow.Add(_dmgLabel);
            _dmg = MakeBar(16, 0);
            _dmgRow.Add(_dmg.root);
            _dmgRow.style.display = DisplayStyle.None;
            _survBox.Add(_dmgRow);

            // 다음 웨이브/보스까지
            _nextRow = new VisualElement { pickingMode = PickingMode.Ignore };
            _nextRow.style.marginTop = 10;
            _nextLabel = Text("", 18, Cream);
            _nextLabel.style.marginBottom = 3;
            _nextRow.Add(_nextLabel);
            _next = MakeBar(12, 0);
            _nextRow.Add(_next.root);
            _nextRow.style.display = DisplayStyle.None;
            _survBox.Add(_nextRow);

            // 중간보스 (4일차마다 섞여 나온다)
            _bossRow = Row();
            _bossRow.style.marginTop = 10;
            _bossCheck = new VisualElement { pickingMode = PickingMode.Ignore };
            _bossCheck.style.width = 24; _bossCheck.style.height = 24; _bossCheck.style.marginRight = 10;
            Radius(_bossCheck, 12);
            Border(_bossCheck, 3, new Color(1f, 0.45f, 0.4f, 0.9f));
            _bossCheck.style.alignItems = Align.Center; _bossCheck.style.justifyContent = Justify.Center;
            VisualElement bossTick = Icon("check", 15f);
            if (bossTick != null) { bossTick.name = "tick"; bossTick.style.opacity = 0f; _bossCheck.Add(bossTick); }
            _bossRow.Add(_bossCheck);
            _bossLabel = Text("중간보스 처치", 20, Cream);
            _bossRow.Add(_bossLabel);
            _bossRow.style.display = DisplayStyle.None;
            _survBox.Add(_bossRow);

            _card.Add(_survBox);

            // ---------------- 탈출 작전 (생존 조건을 모두 돌파한 뒤에야 나타난다)
            if (_hasGate) BuildEscape();

            // 번쩍임 막 (낮/밤이 바뀔 때)
            _flash = new VisualElement { pickingMode = PickingMode.Ignore };
            _flash.style.position = Position.Absolute;
            _flash.style.left = 0; _flash.style.top = 0; _flash.style.right = 0; _flash.style.bottom = 0;
            Radius(_flash, 14);
            _flash.style.backgroundColor = new Color(1f, 0.95f, 0.8f, 1f);
            _flash.style.opacity = 0f;
            if (poly)
            {
                // 판이 없으니 번쩍임도 그늘 모양으로 (네모 상자가 번쩍이지 않게)
                _flash.style.backgroundImage = new StyleBackground(skin.hudFade);
                _flash.style.unityBackgroundImageTintColor = new Color(1f, 0.9f, 0.6f, 1f);
                _flash.style.backgroundColor = Color.clear;
            }
            _card.Add(_flash);

            root.Add(_card);
        }

        private void BuildEscape()
        {
            _escBox = new VisualElement { pickingMode = PickingMode.Ignore };
            _escBox.style.marginTop = 12;
            _escBox.style.display = DisplayStyle.None;
            _escBox.Add(Divider());

            // 탈출 부품 체크리스트
            _partsBox = new VisualElement { pickingMode = PickingMode.Ignore };
            _partsBox.style.marginTop = 8;
            int n = StoryDirector.PartCount;
            _partRow = new VisualElement[n];
            _partCheck = new VisualElement[n];
            _owned = new bool[n];
            _pop = new float[n];
            for (int i = 0; i < n; i++)
            {
                _pop[i] = 1f;
                var row = Row();
                row.style.height = 44;
                row.style.paddingLeft = 6; row.style.paddingRight = 6;
                Radius(row, 10);
                row.style.marginBottom = 4;

                var iconBox = new VisualElement { pickingMode = PickingMode.Ignore };
                iconBox.style.width = 34; iconBox.style.height = 34; iconBox.style.marginRight = 10;
                Sprite sp = StoryDirector.PartIcon(i);
                if (sp != null) iconBox.style.backgroundImage = new StyleBackground(sp);
                row.Add(iconBox);

                Label name = Text(StoryDirector.PartName(i), 23, Cream);
                name.style.flexGrow = 1;
                row.Add(name);

                var check = new VisualElement { pickingMode = PickingMode.Ignore };
                check.style.width = 28; check.style.height = 28;
                Radius(check, 14);
                Border(check, 3, new Color(1f, 1f, 1f, 0.4f));
                check.style.alignItems = Align.Center; check.style.justifyContent = Justify.Center;
                VisualElement tick = Icon("check", 18f);
                if (tick != null) { tick.name = "tick"; tick.style.opacity = 0f; check.Add(tick); }
                row.Add(check);

                _partsBox.Add(row);
                _partRow[i] = row;
                _partCheck[i] = check;
            }
            _escBox.Add(_partsBox);

            // 게이트 방향
            _gateRow = Row();
            _gateRow.style.marginTop = 6;
            _gateRow.style.height = 46;
            _gateRow.style.paddingLeft = 6;
            Radius(_gateRow, 10);

            var arrowBox = new VisualElement { pickingMode = PickingMode.Ignore };
            arrowBox.style.width = 36; arrowBox.style.height = 36; arrowBox.style.marginRight = 10;
            arrowBox.style.alignItems = Align.Center; arrowBox.style.justifyContent = Justify.Center;
            Radius(arrowBox, 18);
            Border(arrowBox, 2, new Color(1f, 1f, 1f, 0.35f));
            _arrow = new Label("▲") { pickingMode = PickingMode.Ignore };
            _arrow.style.unityFontDefinition = _font;
            _arrow.style.fontSize = 20;
            _arrow.style.color = Gold;
            _arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrowBox.Add(_arrow);
            _gateRow.Add(arrowBox);

            _goalLabel = Text("", 22, Cream);
            _goalLabel.style.flexGrow = 1;
            _gateRow.Add(_goalLabel);
            _escBox.Add(_gateRow);

            _card.Add(_escBox);
        }

        private Bar MakeBar(float height, float fontSize)
        {
            var b = new Bar();
            b.root = new VisualElement { pickingMode = PickingMode.Ignore };
            b.root.style.height = height;
            Radius(b.root, height / 2f);
            b.root.style.backgroundColor = new Color(0f, 0f, 0f, 0.4f);
            Border(b.root, 2, new Color(1f, 1f, 1f, 0.2f));
            b.root.style.overflow = Overflow.Hidden;

            b.fill = new VisualElement { pickingMode = PickingMode.Ignore };
            b.fill.style.position = Position.Absolute;
            b.fill.style.left = 0; b.fill.style.top = 0; b.fill.style.bottom = 0;
            b.fill.style.width = new Length(0f, LengthUnit.Percent);
            Radius(b.fill, height / 2f);
            b.root.Add(b.fill);

            if (fontSize > 0f)
            {
                b.text = Text("", fontSize, Cream);
                b.text.style.position = Position.Absolute;
                b.text.style.left = 0; b.text.style.right = 0; b.text.style.top = 0; b.text.style.bottom = 0;
                b.text.style.unityTextAlign = TextAnchor.MiddleCenter;
                b.root.Add(b.text);
            }
            return b;
        }

        /// <summary>번쩍임 색 (투명 테마에서는 그늘 모양 그림의 색조로, 아니면 상자 바탕색으로).</summary>
        private void SetFlashColor(Color c)
        {
            if (_flash.style.backgroundImage.value.sprite != null) _flash.style.unityBackgroundImageTintColor = c;
            else _flash.style.backgroundColor = c;
        }

        private static void SetBar(Bar b, float target, Color color, string label, float dt)
        {
            b.shown = Mathf.Lerp(b.shown, target, 1f - Mathf.Exp(-9f * dt));
            b.fill.style.width = new Length(Mathf.Clamp01(b.shown) * 100f, LengthUnit.Percent);
            b.fill.style.backgroundColor = color;
            if (b.text != null && label != null) b.text.text = label;
        }

        private void BuildRadio(VisualElement root)
        {
            _radio = new VisualElement { pickingMode = PickingMode.Ignore };
            _radio.style.position = Position.Absolute;
            // 화면 아래쪽 가운데에 낮고 좁게: 전투가 벌어지는 화면 가운데를 가리지 않는다 (알림 HudToast는 그 위 190에 뜬다)
            _radio.style.bottom = 40;
            _radio.style.left = new Length(50f, LengthUnit.Percent);
            _radio.style.marginLeft = -360;      // 폭 720의 절반: 가운데 맞춤 (translate는 흔들림/미끄러짐에 쓴다)
            _radio.style.width = 720;
            _radio.style.flexDirection = FlexDirection.Row;
            _radio.style.alignItems = Align.Center;
            _radio.style.paddingLeft = 24; _radio.style.paddingRight = 28;
            _radio.style.paddingTop = 14; _radio.style.paddingBottom = 14;
            LastTruckUiSkin skin = LastTruckUiSkin.Get();
            bool poly = skin != null && skin.polyTheme && skin.hudBand != null;
            if (poly)
            {
                // 원신풍 자막: 판 없이 양 끝이 사라지는 그늘 띠 위에 화자 + 대사
                _radio.style.width = 900;
                _radio.style.marginLeft = -450;
                _radio.style.paddingLeft = 120; _radio.style.paddingRight = 120;
                LastTruckHud.Skin(_radio, skin.hudBand, 1f, Color.white);
            }
            else
            {
                // 다른 HUD 판과 같은 갈색 판 (예전의 푸른 강철판은 화면에서 혼자 튀었다)
                LastTruckHud.Skin(_radio, _cardSkin != null ? _cardSkin : _radioSkin, 0.5f, new Color(0.7f, 0.6f, 0.55f, 0.97f));
                if (_radioSkin == null) _radio.style.backgroundColor = new Color(0.07f, 0.09f, 0.13f, 0.93f);
            }
            _radio.style.opacity = 0f;
            _radio.style.display = DisplayStyle.None;

            // 왼쪽: 이퀄라이저 (신호가 들어오는 느낌)
            var eqBox = new VisualElement { pickingMode = PickingMode.Ignore };
            eqBox.style.flexDirection = FlexDirection.Row;
            eqBox.style.alignItems = Align.Center;
            eqBox.style.justifyContent = Justify.SpaceBetween;
            eqBox.style.width = 64; eqBox.style.height = 52;
            eqBox.style.marginRight = 18;
            eqBox.style.paddingLeft = 8; eqBox.style.paddingRight = 8;
            Radius(eqBox, 12);
            eqBox.style.backgroundColor = poly ? Color.clear : new Color(0.02f, 0.05f, 0.04f, 0.9f);
            _eq = new VisualElement[5];
            for (int i = 0; i < _eq.Length; i++)
            {
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.style.width = 7; bar.style.height = 8;
                Radius(bar, 3);
                bar.style.backgroundColor = Green;
                eqBox.Add(bar);
                _eq[i] = bar;
            }
            _radio.Add(eqBox);

            // 오른쪽: 머리줄(수신 표시 + 화자) + 글
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.flexGrow = 1; col.style.flexShrink = 1;

            var head = Row();
            head.style.marginBottom = 3;
            _dot = new VisualElement { pickingMode = PickingMode.Ignore };
            _dot.style.width = 12; _dot.style.height = 12; _dot.style.marginRight = 8;
            Radius(_dot, 6);
            _dot.style.backgroundColor = new Color(1f, 0.3f, 0.3f, 1f);
            head.Add(_dot);
            _status = Text("수신 중", 15, new Color(1f, 0.45f, 0.45f, 1f));
            _status.style.marginRight = 14;
            head.Add(_status);
            _radioIcon = Icon("chat", 22f);
            if (_radioIcon != null)
            {
                _radioIcon.style.marginRight = 6;
                _radioIcon.style.unityBackgroundImageTintColor = Gold;
                head.Add(_radioIcon);
            }
            _speaker = Text("관제소", 19, Gold);
            head.Add(_speaker);
            col.Add(head);

            _body = Text("", 22, Cream);
            col.Add(_body);
            _radio.Add(col);

            root.Add(_radio);
        }

        // ================================================================ 바깥에서 부르는 함수

        /// <summary>무전/일지 대사를 띄운다 ("[무전] 내용" 꼴). 이미 나오고 있으면 새 대사로 바꾼다.</summary>
        public void ShowBeat(string beat)
        {
            if (string.IsNullOrEmpty(beat)) return;

            string tag = "무전", text = beat;
            if (beat.StartsWith("["))
            {
                int end = beat.IndexOf(']');
                if (end > 0) { tag = beat.Substring(1, end - 1); text = beat.Substring(end + 1).Trim(); }
            }
            _journal = tag == "일지";
            // 일지는 책장 넘기는 소리, 무전은 딸깍 하고 켜지는 소리
            if (_journal) LastTruck.Audio.Sfx.Play("quest_flip", 0.9f, 1.05f, 0.3f);
            else LastTruck.Audio.Sfx.Play("ui_switch", 1f, 0.9f, 0.3f);
            if (_radioIcon != null) _radioIcon.style.display = _journal ? DisplayStyle.None : DisplayStyle.Flex;
            _speaker.text = _journal ? "생존 일지" : "관제소 무전";
            _speaker.style.color = _journal ? new Color(0.9f, 0.82f, 0.62f, 1f) : Gold;

            _full = text;
            _typed = 0f;
            _body.text = "";
            _rt = 0f;
            _rs = RadioState.Intro;
            _radio.style.display = DisplayStyle.Flex;
        }

        /// <summary>생존 조건 제목을 바꾼다 (바뀌면 갈아 끼우는 움직임 + 카드가 번쩍인다).</summary>
        public void SetObjective(string text, bool flash)
        {
            if (text == _objectiveText && !flash) return;
            _objectiveText = text;
            _condTitle.text = text;
            if (flash)
            {
                LastTruck.Audio.Sfx.Play("quest_flip", 1f, 1f, 0.3f);   // 새 임무: 책장 넘기는 소리
                _objFx = 0f;
                _flashFx = 0f;
                SetFlashColor(new Color(1f, 0.95f, 0.8f, 1f));
            }
        }

        // ================================================================ 매 프레임

        public void Tick(GameManager gm)
        {
            if (gm == null) return;
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            TickCard(gm, dt, now);
            TickRadio(dt, now);
        }

        /// <summary>생존 임무 카드를 열고 닫는다 (K 키). 닫혀 있는 것이 기본이다.</summary>
        public void ToggleCard()
        {
            if (_cardOpen) { _pinned = false; _peekUntil = 0f; }   // 열려 있으면(자동으로 열린 것도) 닫는다
            else _pinned = true;
            _cardOpen = _pinned;
            if (_cardOpen) _card.style.display = DisplayStyle.Flex;
        }

        /// <summary>낮/밤이 바뀔 때 카드를 잠깐 열어 보여 준다 (K로 고정해 둔 상태면 그대로 열려 있다).</summary>
        public void PeekCard(float seconds)
        {
            _peekUntil = Mathf.Max(_peekUntil, Time.unscaledTime + seconds);
        }

        public bool CardOpen { get { return _cardOpen; } }

        private bool _suppressCard;

        /// <summary>false면 임무 카드를 숨긴다 (트럭/배낭 창이 열려 있을 때). 무전 자막은 그대로 둔다.</summary>
        public void SetVisible(bool visible)
        {
            _suppressCard = !visible;
            if (!visible) _card.style.display = DisplayStyle.None;
        }

        private void TickCard(GameManager gm, float dt, float now)
        {
            bool night = gm.CurrentPhase == GameManager.GamePhase.Night;
            Color accent = night ? Night : Gold;
            _accent.style.backgroundColor = accent;

            _cardOpen = _pinned || now < _peekUntil;

            // 임무 카드 소리: 펼칠 때 책 펴는 소리, 접을 때 책 덮는 소리 (트럭/배낭 창에 가려 있을 땐 조용히)
            if (_cardOpen != _cardWasOpen)
            {
                _cardWasOpen = _cardOpen;
                if (!_suppressCard) LastTruck.Audio.Sfx.Play(_cardOpen ? "quest_open" : "quest_close", 1f, 1f, 0.2f);
            }

            // 열 때: 왼쪽에서 튀어 들어온다 (살짝 넘쳤다가 자리 잡는다). 닫을 때: 왼쪽으로 빠져나간다.
            _cardIntro = _cardOpen ? Mathf.Min(1f, _cardIntro + dt / 0.75f) : Mathf.Max(0f, _cardIntro - dt / 0.35f);
            float e = _cardOpen ? EaseOutBack(_cardIntro) : EaseOutCubic(_cardIntro);
            _card.style.translate = new Translate((e - 1f) * 380f, 0);
            _card.style.opacity = Mathf.Clamp01(_cardIntro * 4f);
            _card.style.display = !_suppressCard && (_cardIntro > 0.001f || _cardOpen) ? DisplayStyle.Flex : DisplayStyle.None;
            if (_cardIntro <= 0.001f && !_cardOpen) return;   // 닫혀 있으면 카드 안은 갱신하지 않는다

            // 조건 글: 갈아 끼울 때 아래에서 올라오며 나타난다
            _objFx = Mathf.Min(1f, _objFx + dt / 0.5f);
            float oe = EaseOutCubic(_objFx);
            _objBox.style.opacity = oe;
            _objBox.style.translate = new Translate(0, (1f - oe) * 12f);

            // 번쩍임
            _flashFx = Mathf.Min(1f, _flashFx + dt / 0.7f);
            _flash.style.opacity = (1f - EaseOutCubic(_flashFx)) * 0.3f;

            int cycle = gm.CurrentCycle;
            _dayLabel.text = cycle + "일차";
            _dayLabel.style.color = night ? Night : new Color(1f, 1f, 1f, 0.75f);

            bool esc = StoryDirector.EscapeVisible;
            _title.text = esc ? "탈출 작전" : "생존 임무";

            // 일 진행 칸: 지난 날=채움, 오늘=숨 쉬듯 반짝, 앞으로=흐리게. 모든 날을 넘기면 초록.
            if (_pips != null)
            {
                for (int i = 0; i < _pips.Length; i++)
                {
                    int day = i + 1;
                    VisualElement pip = _pips[i];
                    if (esc || day < cycle) { pip.style.backgroundColor = esc ? Green : accent; pip.style.scale = new Scale(Vector3.one); }
                    else if (day == cycle)
                    {
                        float s = 1f + 0.18f * Mathf.Sin(now * 5f);
                        pip.style.backgroundColor = accent;
                        pip.style.scale = new Scale(new Vector3(1f, s * 1.2f, 1f));
                    }
                    else
                    {
                        pip.style.backgroundColor = Dim;
                        pip.style.scale = new Scale(Vector3.one);
                    }
                }
            }

            TickWeather();
            TickSurvival(gm, night, dt, now, accent);

            if (_escBox != null)
            {
                _escBox.style.display = esc ? DisplayStyle.Flex : DisplayStyle.None;
                if (esc) TickGate(gm, dt, now, accent);
            }
        }

        // ---- 날씨

        private void TickWeather()
        {
            WeatherKind w = WeatherDirector.Kind;
            Color c;
            switch (w)
            {
                case WeatherKind.Fog: c = new Color(0.78f, 0.84f, 0.9f, 1f); break;
                case WeatherKind.Rain: c = new Color(0.5f, 0.72f, 1f, 1f); break;
                case WeatherKind.Storm: c = new Color(0.66f, 0.58f, 1f, 1f); break;
                default: c = new Color(1f, 0.86f, 0.4f, 1f); break;
            }
            _wxDot.style.backgroundColor = c;
            _wxLabel.text = "날씨  " + WeatherDirector.Label(w);

            if (w == WeatherKind.Clear)
            {
                _wxHint.style.display = DisplayStyle.None;
                _wxPenalty.style.display = DisplayStyle.None;
                return;
            }
            _wxHint.style.display = DisplayStyle.Flex;
            _wxPenalty.style.display = DisplayStyle.Flex;
            _wxPenalty.text = WeatherDirector.PenaltyText(w);
            bool lit = CraftingSystem.HeldLantern.LitAmount > 0.5f;
            _wxHint.text = lit ? "랜턴 사용 중 — 시야가 트였어요" : WeatherDirector.Hint(w);
            _wxHint.style.color = lit ? Green : new Color(1f, 1f, 1f, 0.6f);
        }

        // ---- 생존 조건

        private void TickSurvival(GameManager gm, bool night, float dt, float now, Color accent)
        {
            SurvivalDirector.View v = SurvivalDirector.GetView(gm);

            _kindTag.text = night ? "생존 조건 · 진행 중" : "오늘 밤 예고";
            _condTitle.style.color = night ? Night : Gold;
            _condDetail.text = v.detail;

            // 달성/실패하면 카드가 초록/빨강으로 번쩍인다
            if (v.state != _lastResult)
            {
                if (v.state != SurvivalResult.Active)
                {
                    SetFlashColor(v.state == SurvivalResult.Success ? new Color(0.5f, 1f, 0.65f, 1f) : new Color(1f, 0.4f, 0.35f, 1f));
                    _flashFx = 0f;
                }
                _lastResult = v.state;
            }

            // 메인 막대: 낮에는 해질 때까지, 밤에는 조건 진행
            Color fill = v.progress >= 0.999f && night ? Green : accent;
            SetBar(_prog, v.progress, fill, v.progressText, dt);

            // 트럭 누적 피해 게이지 (한도에 가까울수록 초록 → 노랑 → 빨강, 가득 차면 깜빡임)
            _dmgRow.style.display = v.showDamage ? DisplayStyle.Flex : DisplayStyle.None;
            if (v.showDamage)
            {
                Color dc = v.damage01 < 0.5f ? Color.Lerp(Green, Gold, v.damage01 * 2f) : Color.Lerp(Gold, Red, (v.damage01 - 0.5f) * 2f);
                if (v.damage01 > 0.8f) dc.a = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(now * 8f));
                SetBar(_dmg, v.damage01, dc, null, dt);
                _dmgLabel.text = v.damageText;
            }

            // 다음 웨이브/보스까지
            _nextRow.style.display = v.showNext ? DisplayStyle.Flex : DisplayStyle.None;
            if (v.showNext)
            {
                SetBar(_next, v.next01, new Color(1f, 0.62f, 0.3f, 1f), null, dt);
                _nextLabel.text = v.nextText;
            }

            // 중간보스 (4일차마다 섞여 나온다)
            bool showBoss = v.bossText != null;
            _bossRow.style.display = showBoss ? DisplayStyle.Flex : DisplayStyle.None;
            if (showBoss)
            {
                if (v.bossDone && !_bossWasDone) _bossPop = 0f;
                _bossWasDone = v.bossDone;
                _bossPop = Mathf.Min(1f, _bossPop + dt / 0.55f);
                float s = 1f;
                if (v.bossDone) s = _bossPop < 0.55f ? Mathf.Lerp(0.3f, 1.4f, _bossPop / 0.55f) : Mathf.Lerp(1.4f, 1f, (_bossPop - 0.55f) / 0.45f);
                _bossCheck.style.scale = new Scale(new Vector3(s, s, 1f));
                Color bc = v.bossDone ? Green : new Color(1f, 0.45f, 0.4f, 0.9f);
                _bossCheck.style.backgroundColor = v.bossDone ? Green : new Color(0, 0, 0, 0);
                _bossCheck.style.borderTopColor = _bossCheck.style.borderBottomColor = _bossCheck.style.borderLeftColor = _bossCheck.style.borderRightColor = bc;
                VisualElement tick = _bossCheck.Q("tick");
                if (tick != null) { tick.style.opacity = v.bossDone ? 1f : 0f; tick.style.unityBackgroundImageTintColor = new Color(0.05f, 0.25f, 0.12f, 1f); }
                _bossLabel.text = v.bossText;
                _bossLabel.style.color = v.bossDone ? Green : Cream;
            }
            else
            {
                _bossWasDone = false;
            }
        }

        private void TickGate(GameManager gm, float dt, float now, Color accent)
        {
            // 부품: 0.25초마다 보관함을 다시 센다. 새로 생기면 체크가 통통 튄다.
            if (now >= _nextPartCheck)
            {
                _nextPartCheck = now + 0.25f;
                for (int i = 0; i < _owned.Length; i++)
                {
                    bool has = StoryDirector.PartOwned(i);
                    if (has && !_owned[i]) { _pop[i] = 0f; _flashFx = 0f; }
                    _owned[i] = has;
                }
            }

            for (int i = 0; i < _owned.Length; i++)
            {
                _pop[i] = Mathf.Min(1f, _pop[i] + dt / 0.55f);
                bool has = _owned[i];
                VisualElement check = _partCheck[i];

                float s = 1f;
                if (has) s = _pop[i] < 0.55f ? Mathf.Lerp(0.3f, 1.4f, _pop[i] / 0.55f) : Mathf.Lerp(1.4f, 1f, (_pop[i] - 0.55f) / 0.45f);
                check.style.scale = new Scale(new Vector3(s, s, 1f));
                check.style.backgroundColor = has ? Green : new Color(0, 0, 0, 0);
                Color border = has ? Green : new Color(1f, 1f, 1f, 0.4f);
                check.style.borderTopColor = check.style.borderBottomColor = check.style.borderLeftColor = check.style.borderRightColor = border;
                VisualElement tick = check.Q("tick");
                if (tick != null) { tick.style.opacity = has ? 1f : 0f; tick.style.unityBackgroundImageTintColor = new Color(0.05f, 0.25f, 0.12f, 1f); }

                // 막 얻은 줄은 초록으로 번쩍였다가 가라앉는다
                float glow = has ? (1f - _pop[i]) * 0.35f + 0.08f : 0f;
                _partRow[i].style.backgroundColor = new Color(Green.r, Green.g, Green.b, glow);
            }

            // 게이트 방향 화살표: 카메라가 보는 방향 기준으로 돈다
            bool open = StoryDirector.GateIsOpen;
            if (StoryDirector.TryGateBearing(out float dist, out float bearing))
            {
                Camera cam = Camera.main;
                float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
                float bob = open ? Mathf.Sin(now * 6f) * 2f : 0f;
                _arrow.style.rotate = new Rotate(new Angle(bearing - yaw, AngleUnit.Degree));
                _arrow.style.translate = new Translate(0, -bob);
                _arrow.style.color = open ? Green : new Color(1f, 0.5f, 0.42f, 1f);

                int meters = Mathf.RoundToInt(dist);
                _goalLabel.text = "게이트 개방!  " + meters + "m";
                _goalLabel.style.color = Green;
            }

            float gp = 0.12f + 0.1f * (0.5f + 0.5f * Mathf.Sin(now * 4f));
            _gateRow.style.backgroundColor = new Color(Green.r, Green.g, Green.b, gp);
        }

        private void TickRadio(float dt, float now)
        {
            if (_rs == RadioState.Hidden) return;
            _rt += dt;

            switch (_rs)
            {
                case RadioState.Intro:
                {
                    // 신호 잡음: 0.4초 동안 좌우로 떨고 깜빡이며 자리 잡는다
                    float p = Mathf.Clamp01(_rt / 0.4f);
                    float jitter = (1f - p) * 9f * (Random.value - 0.5f) * 2f;
                    float flicker = p < 1f ? (Random.value > 0.35f ? 1f : 0.35f) : 1f;
                    _radio.style.opacity = Mathf.Clamp01(p * 2f) * flicker;
                    SetRadioOffset(jitter, (1f - EaseOutCubic(p)) * 28f);
                    AnimateEq(now, 1f);
                    BlinkDot(now);
                    if (p >= 1f) { _rs = RadioState.Typing; _rt = 0f; }
                    break;
                }
                case RadioState.Typing:
                {
                    _typed += dt * 38f;
                    int chars = Mathf.Min(_full.Length, Mathf.FloorToInt(_typed));
                    _body.text = _full.Substring(0, chars) + (chars < _full.Length ? "▌" : "");
                    _radio.style.opacity = 1f;
                    SetRadioOffset(0f, 0f);
                    AnimateEq(now, 1f);
                    BlinkDot(now);
                    if (chars >= _full.Length)
                    {
                        _rs = RadioState.Hold;
                        _rt = 0f;
                        _holdFor = 3.2f + _full.Length * 0.025f;
                        _status.text = _journal ? "기록 완료" : "수신 완료";
                        _status.style.color = Green;
                        _dot.style.backgroundColor = Green;
                    }
                    break;
                }
                case RadioState.Hold:
                    AnimateEq(now, 0f);
                    _dot.style.opacity = 1f;
                    if (_rt >= _holdFor) { _rs = RadioState.Outro; _rt = 0f; }
                    break;
                case RadioState.Outro:
                {
                    float p = Mathf.Clamp01(_rt / 0.55f);
                    _radio.style.opacity = 1f - p;
                    SetRadioOffset(0f, EaseOutCubic(p) * 16f);
                    if (p >= 1f)
                    {
                        _rs = RadioState.Hidden;
                        _radio.style.display = DisplayStyle.None;
                        _status.text = "수신 중";
                        _status.style.color = new Color(1f, 0.45f, 0.45f, 1f);
                        _dot.style.backgroundColor = new Color(1f, 0.3f, 0.3f, 1f);
                    }
                    break;
                }
            }
        }

        private void SetRadioOffset(float x, float y)
        {
            _radio.style.translate = new Translate(x, y);
        }

        private void BlinkDot(float now)
        {
            _dot.style.opacity = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(now * 9f));
            _status.text = _rs == RadioState.Intro ? "신호 잡는 중…" : "수신 중";
        }

        /// <summary>이퀄라이저 막대: 말하는 동안 출렁이고(amount=1), 끝나면 가라앉는다(0).</summary>
        private void AnimateEq(float now, float amount)
        {
            for (int i = 0; i < _eq.Length; i++)
            {
                float n = Mathf.PerlinNoise(now * 7f + i * 3.7f, i * 1.3f);
                float target = Mathf.Lerp(7f, 52f, n) * amount + 7f * (1f - amount);
                float cur = _eq[i].resolvedStyle.height;
                if (float.IsNaN(cur)) cur = 8f;
                _eq[i].style.height = Mathf.Lerp(cur, target, 0.35f);
            }
        }

        // ================================================================ 작은 부품

        private static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }

        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        private static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }

        private static VisualElement Row()
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            return r;
        }

        private Label Text(string text, float size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lt-text");
            l.style.unityFontDefinition = _font;
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        private static VisualElement Icon(string name, float size)
        {
            Sprite s = UiIcons.Get(name);
            if (s == null) return null;
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = size; e.style.height = size; e.style.flexShrink = 0;
            e.style.backgroundImage = new StyleBackground(s);
            return e;
        }

        private VisualElement Divider()
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.style.height = 2;
            d.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
            return d;
        }

        private VisualElement KeyCap(string key)
        {
            var cap = new VisualElement { pickingMode = PickingMode.Ignore };
            cap.style.minWidth = 32; cap.style.height = 32;
            cap.style.alignItems = Align.Center; cap.style.justifyContent = Justify.Center;
            cap.style.backgroundColor = new Color(0.97f, 0.92f, 0.8f, 1f);
            Radius(cap, 8);
            cap.style.borderTopWidth = cap.style.borderLeftWidth = cap.style.borderRightWidth = 2;
            cap.style.borderBottomWidth = 5;
            cap.style.borderTopColor = cap.style.borderLeftColor = cap.style.borderRightColor = cap.style.borderBottomColor = new Color(0.55f, 0.4f, 0.24f, 1f);
            Label k = Text(key, 18, new Color(0.25f, 0.16f, 0.08f, 1f));
            k.style.unityTextOutlineWidth = 0f;
            cap.Add(k);
            return cap;
        }
    }
}
