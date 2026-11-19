using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck.Networking
{
    /// <summary>
    /// LobbyToolkitView: 캐릭터 선택 (대기실의 [캐릭터 변경]).
    /// 왼쪽 큰 초상화, 오른쪽 이름/설명/고유 능력/능력치 막대, 아래쪽에 모든 캐릭터 + [선택].
    /// 고르면 PlayerProfile에 저장하고 LobbyPlayerEntry.RPC_SetCharacter로 방장에게 알린다 (방장이 확정).
    /// </summary>
    public partial class LobbyToolkitView
    {
        #region 캐릭터 선택

        private VisualElement _charDim, _csPortrait, _csSlotRow;
        private Label _csName, _csDesc;
        private Button _csConfirm;
        private readonly List<VisualElement> _csFills = new List<VisualElement>();
        private readonly List<Label> _csValues = new List<Label>();
        private readonly List<CharSlotUi> _csSlots = new List<CharSlotUi>();
        private bool _csSlotsBuilt;
        private int _previewIndex;
        private int _requestedIndex = -1;

        private class CharSlotUi { public Button root; public int index; }

        public bool IsCharacterSelectOpen => IsShown(_charDim);

        private void BuildCharacterSelect()
        {
            _charDim = Modal(out VisualElement w, "cs-panel");
            w.Add(Header("캐릭터 선택", "SURVIVORS", Btn(null, CloseCharacterSelect, null, "close", false, "mn-copy--small")));
            w.Add(Tape());

            var body = Box("cs-body");
            var preview = Box("cs-preview");
            preview.AddToClassList("mn-well");
            _csPortrait = Box("cs-portrait");
            preview.Add(_csPortrait);
            body.Add(preview);

            var info = Box("cs-info");
            info.AddToClassList("mn-well");
            _csName = L("", "cs-name", FontKind.Heavy);
            _csName.enableRichText = true;
            _csDesc = L("", "cs-desc");
            _csDesc.enableRichText = true;
            info.Add(_csName);
            info.Add(_csDesc);
            foreach (string stat in CharacterSelectUI.StatLabels)
            {
                var row = Box("cs-stat");
                row.Add(L(stat, "cs-stat-label"));
                var bar = Box("cs-stat-bar");
                var fill = Box("cs-stat-fill");
                bar.Add(fill);
                row.Add(bar);
                var value = L("-", "cs-stat-value", FontKind.Heavy);
                row.Add(value);
                info.Add(row);
                _csFills.Add(fill);
                _csValues.Add(value);
            }
            body.Add(info);
            w.Add(body);

            var bottom = Box("cs-bottom");
            var slots = Box("cs-slots");
            slots.AddToClassList("mn-well");
            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("mn-scroll");
            _csSlotRow = Box("cs-slot-row");
            scroll.Add(_csSlotRow);
            slots.Add(scroll);
            bottom.Add(slots);
            _csConfirm = Btn("선택", ConfirmCharacter, "primary", null, true, "cs-confirm");
            bottom.Add(_csConfirm);
            w.Add(bottom);
        }

        /// <summary>지금 내 캐릭터: 방장이 확정한 값, 단 [선택] 직후 응답 전까지는 요청한 번호.</summary>
        private int CurrentIndex
        {
            get
            {
                LobbyPlayerEntry local = LobbyPlayerEntry.Local;
                if (local == null) return PlayerProfile.CharacterIndex;
                if (_requestedIndex >= 0 && local.CharacterIndex != _requestedIndex) return _requestedIndex;
                _requestedIndex = -1;
                return local.CharacterIndex;
            }
        }

        /// <summary>새 화면에서는 내 캐릭터가 참가자 칸에 같이 보인다 → 참가자 칸을 다시 그린다.</summary>
        private void RefreshMyCharacter() => RefreshRoom();

        public void OpenCharacterSelect()
        {
            if (_launcher != null && _launcher.State != LauncherState.InRoom) return;
            BuildSlotsIfNeeded();
            Preview(CurrentIndex);
            Show(_charDim, true);
            _charDim.BringToFront();
        }

        public void CloseCharacterSelect() => Show(_charDim, false);

        private void ConfirmCharacter()
        {
            CharacterCatalog catalog = CharacterCatalog.Instance;
            if (catalog == null || catalog.Count == 0) return;
            if (_launcher != null && _launcher.State != LauncherState.InRoom) { CloseCharacterSelect(); return; }

            int index = catalog.ClampIndex(_previewIndex);
            PlayerProfile.CharacterIndex = index;
            LobbyPlayerEntry local = LobbyPlayerEntry.Local;
            if (local != null)
            {
                _requestedIndex = index;
                local.RPC_SetCharacter(index);
            }
            CloseCharacterSelect();
            RefreshMyCharacter();
        }

        private void BuildSlotsIfNeeded()
        {
            if (_csSlotsBuilt) return;
            _csSlotsBuilt = true;
            CharacterCatalog catalog = CharacterCatalog.Instance;
            int count = catalog != null ? catalog.Count : 0;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                CharacterStatsData data = catalog.Get(i);
                var slot = new CharSlotUi { index = i };
                slot.root = new Button(() => Preview(index)) { text = string.Empty, focusable = false };
                slot.root.AddToClassList("cs-slot");
                var p = Box("cs-slot-portrait");
                p.pickingMode = PickingMode.Ignore;
                SetPortrait(p, data != null ? data.portrait : null);
                slot.root.Add(p);
                var n = L(data != null ? Plain(data.DisplayName) : "?", "cs-slot-name", FontKind.Heavy);
                n.enableRichText = true;
                slot.root.Add(n);
                var c = Box("cs-slot-check");
                c.pickingMode = PickingMode.Ignore;
                slot.root.Add(c);
                _csSlotRow.Add(slot.root);
                _csSlots.Add(slot);
            }
            if (count == 0) _csSlotRow.Add(L("캐릭터 목록이 비어 있습니다.", "mn-empty"));
        }

        private void Preview(int index)
        {
            CharacterCatalog catalog = CharacterCatalog.Instance;
            _previewIndex = catalog != null ? catalog.ClampIndex(index) : 0;
            CharacterStatsData data = catalog != null && catalog.Count > 0 ? catalog.Get(_previewIndex) : null;

            int current = CurrentIndex;
            foreach (CharSlotUi s in _csSlots)
            {
                s.root.EnableInClassList("cs-slot--preview", s.index == _previewIndex);
                s.root.EnableInClassList("cs-slot--equipped", s.index == current);
            }

            SetPortrait(_csPortrait, data != null ? data.portrait : null);
            _csName.text = data != null ? Plain(data.DisplayName) : "캐릭터 없음";
            _csDesc.text = data == null
                ? "캐릭터 목록이 비어 있습니다."
                : (string.IsNullOrWhiteSpace(data.description)
                    ? "<color=#9AA3B2>(아직 설명이 없습니다)</color>"
                    : Plain(data.description))
                  + (string.IsNullOrEmpty(data.AbilitySummary) ? string.Empty : "\n\n" + data.AbilitySummary);   // 고유 능력 설명

            for (int i = 0; i < _csFills.Count; i++)
            {
                float value = data != null ? GetStat(data, i) : 0f;
                float max = 0.0001f;
                if (catalog != null)
                    foreach (CharacterStatsData other in catalog.Characters)
                        if (other != null) max = Mathf.Max(max, GetStat(other, i));
                _csFills[i].style.width = Length.Percent(Mathf.Clamp01(value / max) * 100f);
                _csValues[i].text = data != null ? value.ToString("0.##") : "-";
            }

            _csConfirm.SetEnabled(data != null);
        }

        private static float GetStat(CharacterStatsData data, int statIndex)
        {
            switch (statIndex)
            {
                case 0: return data.MaxHealth;
                case 1: return data.AttackPower;
                case 2: return data.MoveSpeed;
                case 3: return data.CostReduction;
                case 4: return data.HarvestSpeed;
                case 5: return data.RepairBonus;
                default: return 0f;
            }
        }

        #endregion
    }
}
