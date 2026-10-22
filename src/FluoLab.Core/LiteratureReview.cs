using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FluoLab.Core
{
    public enum LiteratureReviewState { Unreviewed, Reviewed, Questionable }
    public sealed class LiteratureRevision
    {
        public string SavedAt { get; set; }
        public string MachineText { get; set; }
        public string Draft { get; set; }
        public LiteratureReviewState State { get; set; }
    }
    public sealed class LiteratureReviewRecord
    {
        public string MachineSha256 { get; set; }
        public string Draft { get; set; }
        public LiteratureReviewState State { get; set; }
        public string SavedAt { get; set; }
        public List<LiteratureRevision> History { get; set; }
    }
    public static class LiteratureReview
    {
        public static string StateLabel(LiteratureReviewState state)
        { return state == LiteratureReviewState.Reviewed ? "已人工核对" : state == LiteratureReviewState.Questionable ? "有疑问" : "未核对"; }

        public static bool IsStale(LiteraturePage page)
        { return page.Review != null && page.Review.MachineSha256 != Hash(page.ChineseText); }

        public static void Refresh(LiteratureDocument document)
        {
            foreach (var page in document.Pages)
                if (IsStale(page)) page.Review.State = LiteratureReviewState.Unreviewed;
        }

        public static void Save(LiteraturePage page, string draft, LiteratureReviewState state, DateTimeOffset savedAt)
        {
            if (page == null) throw new ArgumentNullException("page");
            if (!Enum.IsDefined(typeof(LiteratureReviewState), state)) throw new InvalidOperationException("请选择有效的核对状态。");
            draft = draft ?? "";
            if (draft.Length > 100000) throw new InvalidOperationException("单页核对稿超过 10 万字符，请精简后保存。");
            if (state != LiteratureReviewState.Unreviewed && (string.IsNullOrWhiteSpace(page.ChineseText) || string.IsNullOrWhiteSpace(draft)))
                throw new InvalidOperationException("此页必须有机器译文和人工核对稿，才能标为已核对或有疑问。");
            var previous = page.Review;
            var hash = Hash(page.ChineseText);
            if (previous != null && previous.MachineSha256 == hash && previous.Draft == draft && previous.State == state) return;
            var history = previous == null || previous.History == null ? new List<LiteratureRevision>() : previous.History.ToList();
            if (history.Count >= 20) throw new InvalidOperationException("此页已达 20 个修订版本。请先导出记录；本版不会静默删除旧修订。");
            var time = savedAt.ToString("O", CultureInfo.InvariantCulture);
            history.Add(new LiteratureRevision { SavedAt = time, MachineText = page.ChineseText ?? "", Draft = draft, State = state });
            page.Review = new LiteratureReviewRecord { MachineSha256 = hash, Draft = draft, State = state, SavedAt = time, History = history };
        }

        public static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""))).Replace("-", "").ToLowerInvariant();
        }
    }
}
