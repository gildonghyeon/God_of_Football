using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
namespace MyFootballCareer
{
    // 탭 내용: 미션·경기·이적·돈과 시설
    public partial class CareerPrototypeUI
    {
        // ---------- 미션 탭 ----------
        static void SetButton(Button b, string label, bool ready, Color fill, Color quiet)
        {
            if (b == null) return;
            b.GetComponentInChildren<Text>().text = label;
            ((Image)b.targetGraphic).color = ready ? fill : quiet;
        }

        // header: 위 안내, claimed: 이번 7일 중 받은 날 수, today: 오늘 받을 날(1~7, 없으면 0), rewards: 카드 7장의 보상 글, detail: 아래 보상 설명
        // note: 카드의 보상 단위 안내 (강화 1회·수당 1주가 지금 얼마인지)
        public void SetAttendance(string header, int claimed, int today, string[] rewards, string detail, string note, bool ready)
        {
            if (attendText == null) return;
            attendText.text = header;
            attendText.color = ready ? PixelTheme.Accent : PixelTheme.Text;
            if (attendCards != null) attendCards.Set(claimed, today, rewards);
            attendDetail.text = detail;
            attendNote.text = note;
            SetButton(attendButton, ready ? "출석 보상 받기" : "오늘은 받았어요", ready, PixelTheme.AccentFill, PixelTheme.QuietFill);
        }

        // 미션 한 줄: 설명·진행도·보상, 버튼 (받을 수 있음 / 진행 중 / 받음)
        public void SetMission(int index, string info, string button, bool ready)
        {
            if (index < 0 || index >= missionTexts.Length || missionTexts[index] == null) return;
            missionTexts[index].text = info;
            SetButton(missionButtons[index], button, ready, PixelTheme.ButtonFill, PixelTheme.QuietFill);
        }

        public void SetDailyBonus(string reset, string info, string button, bool ready)
        {
            if (bonusText == null) return;
            resetText.text = reset;
            bonusText.text = info;
            SetButton(bonusButton, button, ready, PixelTheme.AccentFill, PixelTheme.QuietFill);
        }

        // 받을 보상이 있으면 미션 탭 이름에 ! 표시
        public void SetMissionBadge(bool on)
        {
            if (tabLabels[2] == null) return;
            tabLabels[2].text = on ? "미션 !" : "미션";
            tabLabels[2].color = on ? PixelTheme.Accent : PixelTheme.Text;
        }
        public void SetLeaders(string text) { if (leadersText != null) SetLines(leadersText, text, 2); }

        static void SetLines(Text t, string text, int minLines)
        {
            t.text = text;
            int lines = Mathf.Max(minLines, text.Split('\n').Length);
            t.GetComponent<LayoutElement>().preferredHeight = lines * 30f + 8f;
        }

        public void SetSeason(bool seasonComplete, string history)
        {
            if (playLabel == null) return;
            if (historyText != null) SetLines(historyText, history, 2);
            playLabel.text = seasonComplete ? "다음 시즌 시작" : "다음 경기";
        }

        // 경기 탭: 다음 경기 카드, 지난 경기 카드, 순위표
        public void SetMatchPage(NextMatchView next, ResultView last, List<TableRowView> table)
        {
            if (matchPage == null) return;
            matchPage.SetNext(next); matchPage.SetResult(last); matchPage.SetTable(table);
        }

        public void SetLeaderBoard(string header, List<LeaderBlockView> blocks, string footer)
        {
            if (matchPage != null) matchPage.SetLeaders(header, blocks, footer);
        }

        // 커리어 탭의 '소속 팀' 카드와 '이적 제안' 카드들. 제안 카드의 숫자 아래에는 지금 팀과의 차이(▲▼)를 보여 줍니다.
        // summary: 시즌 성적 등급과 제안 수 안내 (null이면 아직 시즌 중)
        // decided: 선택을 확정했으면 true. 이때는 버튼을 누를 수 없고 결과만 보여 줍니다.
        List<TransferOffer> tOffers;
        string tPending, tTeamName, tSummary, tCurrentKey;
        bool tDecided;
        TeamInfoView tCurrent;

        public void SetTransfer(List<TransferOffer> offers, string pendingTeamId, string currentTeamName, string summary, bool decided)
        {
            tOffers = offers; tPending = pendingTeamId; tTeamName = currentTeamName; tSummary = summary; tDecided = decided;
            RenderTransfer();
        }

        // 지금 팀 정보 (능력치가 오르면 선발 확률이 바뀌므로 자주 불림). 바뀌었을 때만 다시 그립니다.
        public void SetCurrentTeam(TeamInfoView team)
        {
            string key = team != null ? JsonUtility.ToJson(team) : "";
            if (key == tCurrentKey) return;
            tCurrentKey = key; tCurrent = team;
            RenderTransfer();
        }

        void RenderTransfer()
        {
            if (transferRoot == null) return;
            foreach (Transform child in transferRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var cur = tCurrent;
            Color good = new Color(.55f, .95f, .65f, 1f);
            if (cur != null)
            {
                var section = Section(transferRoot, "소속 팀", true);
                var card = NewRect("Current", section);
                card.gameObject.AddComponent<Image>().color = new Color(.12f, .20f, .34f, 1f);
                ConfigureStack(card.gameObject, 12, 10, 12, 12, 8);
                var head = NewRect("Head", card); Height(head.gameObject, 84);
                TeamKits.PreviewImage(head, TeamKits.Home(cur.teamId), 0f, 0f, .13f, 1f);
                MatchResultDialog.Fit(MatchResultDialog.Label(head, font, cur.name, 24, PixelTheme.Text, .15f, .45f, .78f, .95f, TextAnchor.MiddleLeft), 14);
                MatchResultDialog.Fit(MatchResultDialog.Label(head, font, TeamKits.Name(cur.teamId), 16, PixelTheme.TextDim, .15f, .05f, .78f, .45f, TextAnchor.MiddleLeft), 11);
                MatchResultDialog.Label(head, font, cur.division > 0 ? cur.division + "부" : "", 18, PixelTheme.TextDim, .78f, 0f, 1f, 1f, TextAnchor.MiddleRight);
                var tiles = TileRow(card);
                MiniTile(tiles, "전력", cur.strength.ToString("0"), null, PixelTheme.TextDim);
                MiniTile(tiles, "선발 확률", Mathf.RoundToInt(cur.startChance * 100f) + "%", null, PixelTheme.TextDim);
                MiniTile(tiles, "주급", cur.wage > 0 ? SeasonSystem.Money(cur.wage) : "-", null, PixelTheme.TextDim);
                MiniTile(tiles, "중시 능력", Ko.Attr(cur.focus), null, PixelTheme.TextDim);
                MatchResultDialog.Label(NewRectH(card, 24), font, "내 평점 " + cur.myRating + "  ·  팀 요구 평점 " + cur.required, 16,
                    cur.myRating >= cur.required ? good : PixelTheme.DangerEdge, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
                string curId = cur.teamId;
                PixelButton("선수단 보기", () => { if (OnShowSquad != null) OnShowSquad(curId); }, card, 52f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                    .GetComponentInChildren<Text>().fontSize = 21;
            }

            var offers = tOffers; string pendingTeamId = tPending, currentTeamName = tTeamName; bool decided = tDecided;
            bool none = offers == null || offers.Count == 0;
            if (none && tSummary == null) return;

            var body = Section(transferRoot, "이적 제안", true);
            if (tSummary != null)
            {
                var s = TextNode(tSummary, body, 20); s.color = PixelTheme.Accent; s.alignment = TextAnchor.MiddleCenter;
                s.horizontalOverflow = HorizontalWrapMode.Wrap; Height(s.gameObject, 56);
            }
            if (none) return;   // 제안이 없으면 지금 팀에 남습니다
            string noteText = !decided ? "다음 시즌에 갈 팀을 고르거나 잔류하세요. 선택은 한 번뿐입니다."
                : string.IsNullOrEmpty(pendingTeamId) ? "결정 완료: " + currentTeamName + "에 잔류합니다."
                : "결정 완료: 표시된 팀으로 다음 시즌에 이적합니다.";
            var info = TextNode(noteText, body, 18); info.color = PixelTheme.TextDim; info.alignment = TextAnchor.MiddleCenter;
            info.horizontalOverflow = HorizontalWrapMode.Wrap; Height(info.gameObject, 30);
            if (cur != null)
            {
                var hint = TextNode("▲▼ 는 지금 팀(" + cur.name + ")과 비교한 차이입니다.", body, 16); hint.color = PixelTheme.TextDim;
                hint.alignment = TextAnchor.MiddleCenter; hint.horizontalOverflow = HorizontalWrapMode.Wrap; Height(hint.gameObject, 26);
            }
            foreach (var offer in offers)
            {
                string id = offer.teamId;
                bool agreed = id == pendingTeamId;
                // 제안 카드: 팀 이름 + 숫자 타일(지금 팀과의 차이) + 버튼 두 개
                var card = NewRect("Offer", body);
                card.gameObject.AddComponent<Image>().color = agreed ? new Color(.30f, .24f, .08f, 1f) : new Color(.13f, .18f, .28f, 1f);
                ConfigureStack(card.gameObject, 12, 10, 12, 12, 8);
                var head = NewRect("Head", card); Height(head.gameObject, 84);
                TeamKits.PreviewImage(head, TeamKits.Home(offer.teamId), 0f, 0f, .13f, 1f);
                MatchResultDialog.Fit(MatchResultDialog.Label(head, font, offer.teamName, 24, PixelTheme.Accent, .15f, .45f, .7f, .95f, TextAnchor.MiddleLeft), 14);
                MatchResultDialog.Fit(MatchResultDialog.Label(head, font, TeamKits.Name(offer.teamId), 16, PixelTheme.TextDim, .15f, .05f, .7f, .45f, TextAnchor.MiddleLeft), 11);
                // 부: 지금보다 높은 부면 ▲ 초록, 낮은 부면 ▼ 빨강
                string divText = offer.division > 0 ? offer.division + "부" : "";
                Color divColor = PixelTheme.TextDim;
                if (cur != null && offer.division > 0 && cur.division > 0 && offer.division != cur.division)
                {
                    bool up = offer.division < cur.division;
                    divText = (up ? "▲ " : "▼ ") + divText; divColor = up ? good : PixelTheme.DangerEdge;
                }
                MatchResultDialog.Label(head, font, agreed ? "확정" : divText, 18, agreed ? PixelTheme.Accent : divColor, .7f, 0f, 1f, 1f, TextAnchor.MiddleRight);
                var tiles = TileRow(card);
                Color dc = PixelTheme.TextDim;
                string d1 = cur != null ? Delta(offer.teamStrength - cur.strength, "0", out dc) : null;
                MiniTile(tiles, "전력", offer.teamStrength.ToString("0"), d1, dc);
                string d2 = cur != null ? Delta(Mathf.RoundToInt(offer.startChance * 100f) - Mathf.RoundToInt(cur.startChance * 100f), "0", out dc) : null;
                MiniTile(tiles, "선발 확률", Mathf.RoundToInt(offer.startChance * 100f) + "%", d2 != null && d2 != "=" ? d2 + "%" : d2, dc);
                string d3 = cur != null && offer.weeklyWage > 0 && cur.wage > 0 ? Delta(offer.weeklyWage - cur.wage, "#,0", out dc) : null;
                MiniTile(tiles, "주급", offer.weeklyWage > 0 ? SeasonSystem.Money(offer.weeklyWage) : "-", d3, dc);
                MiniTile(tiles, "중시 능력", Ko.Attr(offer.focus), cur != null ? (offer.focus == cur.focus ? "같음" : "다름") : null, PixelTheme.TextDim);
                var buttons = NewRect("Buttons", card); Height(buttons.gameObject, 56);
                var bh = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
                bh.spacing = 8; bh.childControlWidth = true; bh.childControlHeight = true; bh.childForceExpandWidth = true; bh.childForceExpandHeight = true;
                var button = PixelButton(agreed ? "이적 확정됨" : "이 팀으로 이적", () => onChooseOffer(id), buttons, 0f,
                            agreed ? PixelTheme.AccentFill : decided ? PixelTheme.QuietFill : PixelTheme.ButtonFill,
                            agreed ? PixelTheme.Accent : decided ? PixelTheme.QuietEdge : PixelTheme.ButtonEdge);
                button.GetComponentInChildren<Text>().fontSize = 21;
                if (decided) button.interactable = false;
                PixelButton("선수단 보기", () => { if (OnShowSquad != null) OnShowSquad(id); }, buttons, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                    .GetComponentInChildren<Text>().fontSize = 21;
            }
            var stay = PixelButton((decided && string.IsNullOrEmpty(pendingTeamId) ? "[확정] " : "") + currentTeamName + " 잔류", () => onStay(), body, 68f,
                decided && string.IsNullOrEmpty(pendingTeamId) ? PixelTheme.AccentFill : PixelTheme.QuietFill,
                decided && string.IsNullOrEmpty(pendingTeamId) ? PixelTheme.Accent : PixelTheme.QuietEdge);
            if (decided) stay.interactable = false;
        }

        // 지금 팀과의 차이 글자: 양수 ▲ 초록, 음수 ▼ 빨강, 같으면 "="
        static string Delta(float diff, string format, out Color color)
        {
            if (Mathf.Abs(diff) < 0.5f) { color = PixelTheme.TextDim; return "="; }
            color = diff > 0f ? new Color(.55f, .95f, .65f, 1f) : PixelTheme.DangerEdge;
            return (diff > 0f ? "▲ " : "▼ ") + Mathf.Abs(diff).ToString(format);
        }

        RectTransform TileRow(Transform card)
        {
            var tiles = NewRect("Tiles", card); Height(tiles.gameObject, 72);
            var th = tiles.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 6; th.childControlWidth = true; th.childControlHeight = true; th.childForceExpandWidth = true; th.childForceExpandHeight = true;
            return tiles;
        }

        RectTransform NewRectH(Transform parent, float height) { var r = NewRect("Line", parent); Height(r.gameObject, height); return r; }

        // 카드의 작은 숫자 칸: 값, 차이(없으면 비움), 이름
        void MiniTile(Transform row, string label, string value, string delta, Color deltaColor)
        {
            var tile = NewRect("Tile", row);
            tile.gameObject.AddComponent<Image>().color = new Color(.09f, .12f, .19f, 1f);
            MatchResultDialog.Fit(MatchResultDialog.Label(tile, font, value, 21, PixelTheme.Text, .04f, .50f, .96f, .98f, TextAnchor.MiddleCenter), 12);
            if (!string.IsNullOrEmpty(delta))
                MatchResultDialog.Fit(MatchResultDialog.Label(tile, font, delta, 14, deltaColor, .04f, .27f, .96f, .52f, TextAnchor.MiddleCenter), 10);
            MatchResultDialog.Fit(MatchResultDialog.Label(tile, font, label, 13, PixelTheme.TextDim, .04f, .02f, .96f, .29f, TextAnchor.MiddleCenter), 9);
        }

        // 돈, 주급, 동시 훈련 레벨과 다음 레벨 가격 (price < 0이면 최고 레벨)
        // pay: 경기 한 번에 실제로 받는 돈, agentPercent: 에이전트 보너스(%)
        public void SetMoney(long money, int pay, int agentPercent)
        {
            if (facilityMoneyText != null) facilityMoneyText.text = "보유 돈 " + SeasonSystem.Money(money);
            if (moneyText == null) return;
            moneyText.text = "돈 " + SeasonSystem.Money(money) + "\n경기당 " + SeasonSystem.Money(pay) + " (주급 1주치)"
                + (agentPercent > 0 ? "  에이전트 +" + agentPercent + "% 포함" : "");
        }

        // 시설 버튼: 글자와 살 수 있는지 (살 수 있으면 강조)
        public void SetFacility(int index, string text, bool affordable)
        {
            if (index < 0 || index >= facilityButtons.Length || facilityButtons[index] == null) return;
            facilityButtons[index].GetComponentInChildren<Text>().text = text;
            ((Image)facilityButtons[index].targetGraphic).color = affordable ? PixelTheme.AccentFill : PixelTheme.QuietFill;
        }

        public void SetRebirth(string text, bool available)
        {
            if (rebirthText != null) SetLines(rebirthText, text, 4);
            if (rebirthFill != null) rebirthFill.color = available ? PixelTheme.RebirthFill : PixelTheme.QuietFill;
        }
    }
}
