using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck.Networking
{
    /// <summary>
    /// LobbyToolkitView: 대기실 화면.
    ///  - 왼쪽: 방 이름(방장은 눌러서 입력칸으로 바꾸기 → Enter/다른 곳 클릭 때 저장, Esc 취소), 공개/인원, 참가자 4칸
    ///  - 오른쪽: 방 코드 + 복사, 비밀번호(비공개 방만) + 복사, 캐릭터 변경 / 아래쪽에 방 나가기 + 게임 시작(방장)·준비(참가자)
    /// </summary>
    public partial class LobbyToolkitView
    {
        #region 대기실 화면

        private VisualElement _roomTitleBox, _pencil, _slotList, _pwBlock;
        private Label _roomTitleLabel, _roomInfo, _roomCodeLabel, _roomPwLabel, _copyToast, _roomStatus;
        private TextField _roomTitleField;
        private Button _startBtn, _readyBtn, _leaveBtn, _changeCharBtn;
        private readonly List<SlotUi> _slots = new List<SlotUi>();
        private float _refreshTimer;
        private bool _renaming;

        private class SlotUi
        {
            public VisualElement root, filled, empty, portrait;
            public Label name, host, character, pill;
        }

        private VisualElement BuildRoomScreen()
        {
            var screen = Box("mn-screen");
            screen.Add(Box("mn-shade"));
            var center = Box("mn-center");
            screen.Add(center);
            var two = Box("mn-two");
            center.Add(two);

            // ---- 왼쪽 ----
            var left = Plate("mn-left");
            _roomTitleBox = Box("mn-room-title");
            _roomTitleLabel = L("", "mn-room-title-text", FontKind.Heavy);
            _roomTitleLabel.enableRichText = true;
            _roomTitleBox.Add(_roomTitleLabel);
            _pencil = Box("mn-pencil");
            _pencil.pickingMode = PickingMode.Ignore;
            _roomTitleBox.Add(_pencil);
            _roomTitleField = Field("mn-field--title", LobbyRules.TitleMaxLength);
            _roomTitleField.style.width = 760;
            _roomTitleField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) EndRename(true);
            }, TrickleDown.TrickleDown);
            _roomTitleField.RegisterCallback<FocusOutEvent>(_ => { if (_renaming) EndRename(true); });
            Show(_roomTitleField, false);
            _roomTitleBox.Add(_roomTitleField);
            _roomTitleBox.RegisterCallback<ClickEvent>(_ => BeginRename());
            var titleRow = Row(null);
            titleRow.Add(_roomTitleBox);
            left.Add(titleRow);

            _roomInfo = L("", "mn-label");
            _roomInfo.style.marginTop = 8;
            _roomInfo.style.marginLeft = 18;
            left.Add(_roomInfo);
            left.Add(Tape());

            _slotList = Box(null);
            for (int i = 0; i < LobbyRules.MaxPlayersLimit; i++) _slots.Add(MakeSlot());
            left.Add(_slotList);
            two.Add(left);

            // ---- 오른쪽 ----
            var right = Plate("mn-right");
            right.Add(L("방 코드", "mn-label"));
            var codeRow = Row(null);
            codeRow.style.marginTop = 10;
            _roomCodeLabel = L("------", "mn-codebox", FontKind.Stencil);
            codeRow.Add(_roomCodeLabel);
            var copyCode = Btn(null, CopyRoomCode, null, "copy", false, "mn-copy");
            codeRow.Add(copyCode);
            right.Add(codeRow);

            _pwBlock = Box(null);
            var pwTitle = L("비밀번호", "mn-label");
            pwTitle.style.marginTop = 26;
            _pwBlock.Add(pwTitle);
            var pwRow = Row(null);
            pwRow.style.marginTop = 10;
            _roomPwLabel = L("", "mn-pwbox", FontKind.Heavy);
            _roomPwLabel.enableRichText = true;
            pwRow.Add(_roomPwLabel);
            pwRow.Add(Btn(null, CopyRoomPassword, null, "copy", false, "mn-copy mn-copy--small"));
            _pwBlock.Add(pwRow);
            right.Add(_pwBlock);

            _copyToast = L("", "mn-toast");
            right.Add(_copyToast);
            right.Add(L("복사 아이콘을 누르면 클립보드에 복사돼요", "mn-small"));
            right.Add(Box("mn-sep"));
            _changeCharBtn = Btn("캐릭터 변경", OpenCharacterSelect, null, "user");
            right.Add(_changeCharBtn);

            var bottom = Box("mn-room-bottom");
            _roomStatus = L("", "mn-status");
            bottom.Add(_roomStatus);
            _leaveBtn = Btn("방 나가기", () => { if (_launcher != null) _launcher.RequestLeaveSession(); }, "red");
            _leaveBtn.style.marginBottom = 16;
            bottom.Add(_leaveBtn);
            _startBtn = Btn("게임 시작", () => { if (_launcher != null) _launcher.StartGameAsHost(); }, "primary", null, true);
            _readyBtn = Btn("준비", OnReadyClicked, "primary", null, true);
            foreach (Button b in new[] { _startBtn, _readyBtn })
            {
                b.style.height = 96;
                b.style.fontSize = 36;
                bottom.Add(b);
            }
            right.Add(bottom);
            two.Add(right);
            return screen;
        }

        private SlotUi MakeSlot()
        {
            var s = new SlotUi();
            s.root = Box(null);
            s.filled = Box("mn-slot");
            s.filled.AddToClassList("mn-well");
            s.portrait = Box("mn-portrait");
            s.filled.Add(s.portrait);
            var info = Box("mn-grow");
            var nameRow = Row(null);
            s.name = L("", "mn-slot-name", FontKind.Heavy);
            s.name.enableRichText = true;
            s.host = L("★ 방장", "mn-slot-host", FontKind.Heavy);
            nameRow.Add(s.name);
            nameRow.Add(s.host);
            info.Add(nameRow);
            s.character = L("", "mn-slot-char");
            s.character.enableRichText = true;
            info.Add(s.character);
            s.filled.Add(info);
            s.pill = L("", "mn-pill", FontKind.Heavy);
            s.filled.Add(s.pill);
            s.root.Add(s.filled);

            s.empty = Box("mn-slot");
            s.empty.AddToClassList("mn-slot--empty");
            var emptyLabel = L("빈 자리", "mn-h2");
            emptyLabel.style.color = new Color(0.49f, 0.42f, 0.36f);
            s.empty.Add(emptyLabel);
            s.root.Add(s.empty);
            _slotList.Add(s.root);
            return s;
        }

        #endregion

        #region 갱신

        private void RefreshRoom()
        {
            if (_launcher == null || _screen != MenuScreen.Room) return;

            bool isHost = _launcher.IsHost;
            if (!_renaming) _roomTitleLabel.text = Plain(_launcher.RoomTitle);
            bool canEdit = isHost && _launcher.State == LauncherState.InRoom;
            Show(_pencil, canEdit && !_renaming);
            _roomTitleBox.EnableInClassList("mn-room-title--edit", canEdit && !_renaming);
            if (!canEdit && _renaming) EndRename(false);

            int max = Mathf.Max(1, _launcher.RoomMaxPlayers);
            _roomInfo.text = $"{(_launcher.RoomIsPrivate ? "비공개" : "공개")} 방 · {_launcher.RoomPlayerCount} / {max}명"
                             + (canEdit ? "   ·   방 이름을 누르면 바꿀 수 있어요" : string.Empty);

            string code = _launcher.RoomCode;
            _roomCodeLabel.text = string.IsNullOrEmpty(code) ? "------" : code;
            string pw = _launcher.RoomPassword;
            Show(_pwBlock, _launcher.RoomIsPrivate);
            _roomPwLabel.text = string.IsNullOrEmpty(pw) ? "-" : Plain(pw);

            List<LobbyPlayerEntry> entries = LobbyPlayerEntry.All
                .Where(e => e != null && e.IsAlive)
                .OrderByDescending(e => (bool)e.IsHost)
                .ThenBy(e => e.Owner.PlayerId)
                .ToList();

            CharacterCatalog catalog = CharacterCatalog.Instance;
            for (int i = 0; i < _slots.Count; i++)
            {
                SlotUi s = _slots[i];
                bool inRange = i < max;
                Show(s.root, inRange);
                if (!inRange) continue;
                bool filled = i < entries.Count;
                Show(s.filled, filled);
                Show(s.empty, !filled);
                if (!filled) continue;

                LobbyPlayerEntry e = entries[i];
                bool host = e.IsHost, ready = e.IsReady;
                string safeName = Plain(e.DisplayName);
                s.name.text = e.IsLocal ? $"{safeName} <color=#E8A14A>(나)</color>" : safeName;
                Show(s.host, host);
                SetPortrait(s.portrait, catalog != null ? catalog.GetPortrait(e.CharacterIndex) : null);
                s.character.text = catalog != null ? Plain(catalog.GetDisplayName(e.CharacterIndex)) : string.Empty;
                s.pill.text = host ? "방장" : ready ? "준비 완료" : "대기 중";
                s.pill.EnableInClassList("mn-pill--host", host);
                s.pill.EnableInClassList("mn-pill--ready", !host && ready);
            }

            bool canInteract = _launcher.State == LauncherState.InRoom;
            Show(_startBtn, isHost);
            Show(_readyBtn, !isHost);
            _leaveBtn.SetEnabled(canInteract);
            _changeCharBtn.SetEnabled(canInteract);

            if (isHost)
            {
                bool allReady = _launcher.AreAllPlayersReady();
                _startBtn.SetEnabled(canInteract && allReady);
                int others = entries.Count(e => !e.IsHost);
                int readyOthers = entries.Count(e => !e.IsHost && e.IsReady);
                _roomStatus.text = others == 0
                    ? "혼자서도 시작할 수 있어요."
                    : allReady
                        ? "모두 준비됐어요. 게임을 시작하세요!"
                        : $"모두 준비하면 시작할 수 있어요. ({readyOthers}/{others})";
            }
            else
            {
                LobbyPlayerEntry local = LobbyPlayerEntry.Local;
                bool ready = local != null && local.IsReady;
                _readyBtn.SetEnabled(canInteract && local != null);
                SetText(_readyBtn, ready ? "준비 취소" : "준비");
                _readyBtn.EnableInClassList("mn-btn--primary", !ready);
                _roomStatus.text = ready ? "방장이 게임을 시작하기를 기다리는 중..." : "준비 버튼을 눌러 주세요.";
            }
        }

        private void OnReadyClicked()
        {
            LobbyPlayerEntry local = LobbyPlayerEntry.Local;
            if (local == null || local.IsHost || _launcher == null) return;
            _launcher.SetLocalReady(!local.IsReady);
        }

        #endregion

        #region 방 이름 바꾸기 (방장)

        private void BeginRename()
        {
            if (_renaming || _launcher == null || !_launcher.IsHost || _launcher.State != LauncherState.InRoom) return;
            _renaming = true;
            Show(_roomTitleLabel, false);
            Show(_pencil, false);
            Show(_roomTitleField, true);
            _roomTitleBox.RemoveFromClassList("mn-room-title--edit");
            _roomTitleField.SetValueWithoutNotify(_launcher.RoomTitle);
            _roomTitleField.schedule.Execute(() =>
            {
                _roomTitleField.Focus();
                _roomTitleField.SelectAll();
            }).StartingIn(20);
        }

        private void EndRename(bool save)
        {
            if (!_renaming) return;
            _renaming = false;
            string value = _roomTitleField.value;
            Show(_roomTitleField, false);
            Show(_roomTitleLabel, true);
            if (save && _launcher != null && !_launcher.RenameRoom(value, out string error))
                SystemUI.Alert("방 이름", error);
            RefreshRoom();
        }

        #endregion

        #region 복사

        private void CopyRoomCode()
        {
            string code = _launcher != null ? _launcher.RoomCode : null;
            if (string.IsNullOrEmpty(code)) { Flash(_copyToast, "방 코드가 아직 없어요."); return; }
            CopyToClipboard(code);
            Flash(_copyToast, $"방 코드 {code}를 복사했어요");
        }

        private void CopyRoomPassword()
        {
            string pw = _launcher != null ? _launcher.RoomPassword : null;
            if (string.IsNullOrEmpty(pw)) { Flash(_copyToast, "복사할 비밀번호가 없어요."); return; }
            CopyToClipboard(pw);
            Flash(_copyToast, "비밀번호를 복사했어요");
        }

        #endregion
    }
}
