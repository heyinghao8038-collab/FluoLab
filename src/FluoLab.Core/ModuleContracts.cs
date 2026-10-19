using System;
using System.Collections.Generic;
using System.Threading;

namespace FluoLab.Core
{
    public interface IAnalysisModule
    {
        string Id { get; }
        string DisplayName { get; }
        bool IsAvailable { get; }
        ModuleResult Analyze(SpectrumBatch batch, CancellationToken cancellationToken);
    }

    public sealed class ModuleResult
    {
        public ModuleResult(string moduleId, string summary, IDictionary<string, object> values)
        {
            ModuleId = moduleId;
            Summary = summary;
            Values = values ?? new Dictionary<string, object>();
        }

        public string ModuleId { get; private set; }
        public string Summary { get; private set; }
        public IDictionary<string, object> Values { get; private set; }
    }

    public sealed class GenericOrganizerModule : IAnalysisModule
    {
        public string Id { get { return "generic-organizer"; } }
        public string DisplayName { get { return "通用整理"; } }
        public bool IsAvailable { get { return true; } }

        public ModuleResult Analyze(SpectrumBatch batch, CancellationToken cancellationToken)
        {
            if (batch == null)
            {
                throw new ArgumentNullException("batch");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new ModuleResult(
                Id,
                string.Format("已读取 {0} 份有效文件，跳过 {1} 份异常文件。", batch.SuccessCount, batch.SkippedCount),
                new Dictionary<string, object>
                {
                    { "DiscoveredCount", batch.DiscoveredCount },
                    { "SuccessCount", batch.SuccessCount },
                    { "SkippedCount", batch.SkippedCount }
                });
        }
    }
}

