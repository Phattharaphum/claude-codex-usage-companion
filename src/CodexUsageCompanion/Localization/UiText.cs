using System.Globalization;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.RateLimits;

namespace CodexUsageCompanion.Localization;

public sealed record UiText(
    UiLanguage Language,
    string FiveHourTitle,
    string WeeklyTitle,
    string WaitingForData,
    string LimitUnavailable,
    string ResetUnavailable,
    string MinimizeAction,
    string RefreshAction,
    string HideToTrayAction,
    string TrayShowAction,
    string TrayQuitAction,
    string CloseAction,
    string SettingsAction,
    string SettingsTitle,
    string SystemTrayOption,
    string StartOnBootOption,
    string LanguageOption,
    string ThemeOption,
    string DarkTheme,
    string LightTheme,
    string SystemTheme,
    string PositionOption,
    string UsageLoggingOption,
    string UsageLogFilePathOption,
    string UsageLogFormatOption,
    string UpdateIntervalOption,
    string ResetDateTimeFormatOption,
    string LastUpdatedDateTimeFormatOption,
    string FormatPreview,
    string InvalidDateTimeFormat,
    string AlwaysOnTopOption,
    string SaveAction,
    string CancelAction,
    string EnglishLanguage,
    string TraditionalChineseLanguage,
    string SimplifiedChineseLanguage,
    string AuthorInfo)
{
    public string ResetDateTimeFormat { get; init; } = DateTimeFormatOptions.MonthDay;
    public string LastUpdatedDateTimeFormat { get; init; } =
        DateTimeFormatOptions.HourMinute;

    public static UiText For(
        UiLanguage language,
        string? resetDateTimeFormat = null,
        string? lastUpdatedDateTimeFormat = null)
    {
        var text = language switch
        {
            UiLanguage.TraditionalChinese => new UiText(
                language,
                "5 小時使用量限制",
                "每週使用上限",
                "等待使用量資料",
                "目前方案未提供此額度",
                "重置時間未提供",
                "最小化",
                "重新整理使用量",
                "隱藏至系統匣",
                "顯示視窗",
                "結束",
                "關閉",
                "設定",
                "設定 - Claude Codex Usage Companion",
                "啟用系統匣",
                "開機時自動啟動",
                "語言",
                "主題",
                "深色",
                "淺色",
                "跟隨系統設定",
                "視窗位置",
                "啟用用量更新記錄",
                "記錄檔路徑",
                "記錄格式",
                "更新間隔",
                "重置日期時間格式",
                "最後更新日期時間格式",
                "預覽",
                "日期時間格式無效",
                "視窗永遠置頂",
                "儲存",
                "取消",
                "English (en-US)",
                "繁體中文 (zh-tw)",
                "简体中文 (zh-cn)",
                "作者：ychsieh95 • 原始專案：gkfriend/codex-usage-companion"),
            UiLanguage.SimplifiedChinese => new UiText(
                language,
                "5 小时使用量限制",
                "每周使用上限",
                "等待使用量数据",
                "当前方案未提供此额度",
                "未提供重置时间",
                "最小化",
                "刷新使用量",
                "隐藏到系统托盘",
                "显示窗口",
                "退出",
                "关闭",
                "设置",
                "设置 - Claude Codex Usage Companion",
                "启用系统托盘",
                "开机时自动启动",
                "语言",
                "主题",
                "深色",
                "浅色",
                "跟随系统设置",
                "窗口位置",
                "启用用量更新日志",
                "日志文件路径",
                "日志格式",
                "更新间隔",
                "重置日期时间格式",
                "最后更新日期时间格式",
                "预览",
                "日期时间格式无效",
                "窗口始终置顶",
                "保存",
                "取消",
                "English (en-US)",
                "繁體中文 (zh-tw)",
                "简体中文 (zh-cn)",
                "作者：ychsieh95 • 原始项目：gkfriend/codex-usage-companion"),
            _ => new UiText(
                language,
                "5-hour usage limit",
                "Weekly usage limit",
                "Waiting for usage data",
                "Not available on this plan",
                "Reset time unavailable",
                "Minimize",
                "Refresh usage",
                "Hide to system tray",
                "Show window",
                "Quit",
                "Close",
                "Settings",
                "Settings - Claude Codex Usage Companion",
                "Enable system tray",
                "Start automatically when I sign in",
                "Language",
                "Theme",
                "Dark",
                "Light",
                "Follow system settings",
                "Window position",
                "Enable usage update logging",
                "Log file path",
                "Log format",
                "Update interval",
                "Reset date/time format",
                "Last updated date/time format",
                "Preview",
                "Invalid date/time format",
                "Keep window always on top",
                "Save",
                "Cancel",
                "English (en-US)",
                "繁體中文 (zh-tw)",
                "简体中文 (zh-cn)",
                "Author: ychsieh95 • Original project: gkfriend/codex-usage-companion")
        };
        return text with
        {
            ResetDateTimeFormat = DateTimeFormatOptions.NormalizeReset(
                resetDateTimeFormat),
            LastUpdatedDateTimeFormat = DateTimeFormatOptions.NormalizeLastUpdated(
                lastUpdatedDateTimeFormat)
        };
    }

    public string FormatRemaining(int remainingPercent)
    {
        return Language == UiLanguage.English
            ? $"{remainingPercent}% remaining"
            : Language == UiLanguage.SimplifiedChinese
                ? $"剩余 {remainingPercent}%"
                : $"剩餘 {remainingPercent}%";
    }

    public string RemainingUnavailable => Language == UiLanguage.English
        ? "-- remaining"
        : Language == UiLanguage.SimplifiedChinese
            ? "剩余 --"
            : "剩餘 --";

    public string AntigravityTitle => "Antigravity";

    public string UsageHistoryAction => Language switch
    {
        UiLanguage.TraditionalChinese => "使用記錄",
        UiLanguage.SimplifiedChinese => "使用历史",
        _ => "Usage history"
    };

    public string ResetEfficiencyAction => Language switch
    {
        UiLanguage.TraditionalChinese => "重置效率",
        UiLanguage.SimplifiedChinese => "重置效率",
        _ => "Reset efficiency"
    };

    public string ResetEfficiencyTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "重置效率 - Claude Codex Usage Companion",
        UiLanguage.SimplifiedChinese => "重置效率 - Claude Codex Usage Companion",
        _ => "Reset efficiency - Claude Codex Usage Companion"
    };

    public string UsageHistoryTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "使用記錄 - Claude Codex Usage Companion",
        UiLanguage.SimplifiedChinese => "使用历史 - Claude Codex Usage Companion",
        _ => "Usage history - Claude Codex Usage Companion"
    };

    public string UsageHistoryDashboardTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "用量概覽",
        UiLanguage.SimplifiedChinese => "用量概览",
        _ => "Usage overview"
    };

    public string UsageHistorySummaryTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "所選期間摘要",
        UiLanguage.SimplifiedChinese => "所选期间摘要",
        _ => "Selected period summary"
    };

    public string UsageHistorySummarySubtitle => Language switch
    {
        UiLanguage.TraditionalChinese => "僅依本機記錄估算，額度重置造成的回升不計為使用量",
        UiLanguage.SimplifiedChinese => "仅根据本地记录估算，额度重置造成的回升不计为使用量",
        _ => "Estimated from local samples; quota recovery at resets is excluded"
    };

    public string UsageHistoryChartSubtitle => Language switch
    {
        UiLanguage.TraditionalChinese => "移動游標查看資料點；橘色虛線代表偵測到重置",
        UiLanguage.SimplifiedChinese => "移动光标查看数据点；橙色虚线表示检测到重置",
        _ => "Hover for details; orange dashed lines mark detected resets"
    };

    public string UsageHistoryShowFullPeriod => Language switch
    {
        UiLanguage.TraditionalChinese => "顯示完整期間",
        UiLanguage.SimplifiedChinese => "显示完整期间",
        _ => "Show full period"
    };

    public string UsageHistoryDataOnly => Language switch
    {
        UiLanguage.TraditionalChinese => "僅顯示有資料區段",
        UiLanguage.SimplifiedChinese => "仅显示有数据区段",
        _ => "Data span only"
    };

    public string UsageHistoryPreviousPeriod => Language switch
    {
        UiLanguage.TraditionalChinese => "上一個期間 (Alt+←)",
        UiLanguage.SimplifiedChinese => "上一个期间 (Alt+←)",
        _ => "Previous period (Alt+←)"
    };

    public string UsageHistoryNextPeriod => Language switch
    {
        UiLanguage.TraditionalChinese => "下一個期間 (Alt+→)",
        UiLanguage.SimplifiedChinese => "下一个期间 (Alt+→)",
        _ => "Next period (Alt+→)"
    };

    public string UsageHistoryToday => Language switch
    {
        UiLanguage.TraditionalChinese => "今天",
        UiLanguage.SimplifiedChinese => "今天",
        _ => "Today"
    };

    public string UsageHistoryAllProviders => Language switch
    {
        UiLanguage.TraditionalChinese => "全部提供者",
        UiLanguage.SimplifiedChinese => "全部提供商",
        _ => "All Providers"
    };

    public string UsageHistoryNoPeriodData => Language switch
    {
        UiLanguage.TraditionalChinese => "此日期範圍沒有資料。可切換日期或確認已啟用 CSV 用量記錄。",
        UiLanguage.SimplifiedChinese => "此日期范围没有数据。可切换日期或确认已启用 CSV 用量日志。",
        _ => "No data was recorded in this period. Choose another date or check that CSV usage logging is enabled."
    };

    public string UsageHistoryAnalyzeAction => Language switch
    {
        UiLanguage.TraditionalChinese => "分析期間",
        UiLanguage.SimplifiedChinese => "分析期间",
        _ => "Analyze period"
    };

    public string UsageHistorySamples => Language switch
    {
        UiLanguage.TraditionalChinese => "有效樣本",
        UiLanguage.SimplifiedChinese => "有效样本",
        _ => "Valid samples"
    };

    public string UsageHistoryObservedTime => Language switch
    {
        UiLanguage.TraditionalChinese => "觀察時間",
        UiLanguage.SimplifiedChinese => "观察时间",
        _ => "Observed span"
    };

    public string UsageHistoryDataCoverage => Language switch
    {
        UiLanguage.TraditionalChinese => "第一筆到最後一筆資料",
        UiLanguage.SimplifiedChinese => "第一条到最后一条数据",
        _ => "First to last sample"
    };

    public string UsageHistoryFiveHourConsumed => Language switch
    {
        UiLanguage.TraditionalChinese => "5 小時用量",
        UiLanguage.SimplifiedChinese => "5 小时用量",
        _ => "5-hour consumed"
    };

    public string UsageHistoryWeeklyConsumed => Language switch
    {
        UiLanguage.TraditionalChinese => "每週用量",
        UiLanguage.SimplifiedChinese => "每周用量",
        _ => "Weekly consumed"
    };

    public string UsageHistoryResetsDetected => Language switch
    {
        UiLanguage.TraditionalChinese => "偵測到重置",
        UiLanguage.SimplifiedChinese => "检测到重置",
        _ => "Resets detected"
    };

    public string UsageHistoryResetMarkersHint => Language switch
    {
        UiLanguage.TraditionalChinese => "圖表上的橘色標記",
        UiLanguage.SimplifiedChinese => "图表上的橙色标记",
        _ => "Orange markers on chart"
    };

    public string FormatUsageHistoryProviders(int count) => Language switch
    {
        UiLanguage.TraditionalChinese => $"{count} 個來源",
        UiLanguage.SimplifiedChinese => $"{count} 个来源",
        _ => $"{count} provider{(count == 1 ? string.Empty : "s")}"
    };

    public string FormatUsageHistoryBurnRate(double rate) => Language switch
    {
        UiLanguage.TraditionalChinese => $"平均 {rate:0.##}% / 小時",
        UiLanguage.SimplifiedChinese => $"平均 {rate:0.##}% / 小时",
        _ => $"Average {rate:0.##}% / hour"
    };

    public string UsageHistoryBurnRateIdle => Language switch
    {
        UiLanguage.TraditionalChinese => "閒置",
        UiLanguage.SimplifiedChinese => "闲置",
        _ => "Idle"
    };

    public string UsageHistoryBurnRateLight => Language switch
    {
        UiLanguage.TraditionalChinese => "偏低",
        UiLanguage.SimplifiedChinese => "较低",
        _ => "Light"
    };

    public string UsageHistoryBurnRateModerate => Language switch
    {
        UiLanguage.TraditionalChinese => "中等",
        UiLanguage.SimplifiedChinese => "中等",
        _ => "Moderate"
    };

    public string UsageHistoryBurnRateHigh => Language switch
    {
        UiLanguage.TraditionalChinese => "偏高",
        UiLanguage.SimplifiedChinese => "较高",
        _ => "High"
    };

    public string FormatUsageHistoryVisibleRecords(int visible, int successful) => Language switch
    {
        UiLanguage.TraditionalChinese => $"顯示 {visible} 筆 · {successful} 筆有效樣本",
        UiLanguage.SimplifiedChinese => $"显示 {visible} 条 · {successful} 条有效样本",
        _ => $"Showing {visible} records · {successful} valid samples"
    };

    public string UsageHistoryRecords => Language switch
    {
        UiLanguage.TraditionalChinese => "記錄時間軸",
        UiLanguage.SimplifiedChinese => "记录时间线",
        _ => "History timeline"
    };

    public string UsageHistoryShowTimeline => Language switch
    {
        UiLanguage.TraditionalChinese => "顯示表格",
        UiLanguage.SimplifiedChinese => "显示表格",
        _ => "Show table"
    };

    public string UsageHistoryChartTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "剩餘用量趨勢",
        UiLanguage.SimplifiedChinese => "剩余用量趋势",
        _ => "Remaining quota trend"
    };

    public string UsageHistoryTimeRange => Language switch
    {
        UiLanguage.TraditionalChinese => "時間範圍",
        UiLanguage.SimplifiedChinese => "时间范围",
        _ => "Time range"
    };

    public string UsageHistoryQuotaWindow => Language switch
    {
        UiLanguage.TraditionalChinese => "額度週期",
        UiLanguage.SimplifiedChinese => "额度周期",
        _ => "Quota window"
    };

    public string UsageHistory24Hours => Language switch
    {
        UiLanguage.TraditionalChinese => "24 小時",
        UiLanguage.SimplifiedChinese => "24 小时",
        _ => "24 hours"
    };

    public string UsageHistory7Days => Language switch
    {
        UiLanguage.TraditionalChinese => "7 天",
        UiLanguage.SimplifiedChinese => "7 天",
        _ => "7 days"
    };

    public string UsageHistory30Days => Language switch
    {
        UiLanguage.TraditionalChinese => "30 天",
        UiLanguage.SimplifiedChinese => "30 天",
        _ => "30 days"
    };

    public string UsageHistoryAll => Language switch
    {
        UiLanguage.TraditionalChinese => "全部",
        UiLanguage.SimplifiedChinese => "全部",
        _ => "All"
    };

    public string UsageHistoryWeeklyWindow => Language switch
    {
        UiLanguage.TraditionalChinese => "每週",
        UiLanguage.SimplifiedChinese => "每周",
        _ => "Weekly"
    };

    public string UsageHistoryFiveHourWindow => Language switch
    {
        UiLanguage.TraditionalChinese => "5 小時",
        UiLanguage.SimplifiedChinese => "5 小时",
        _ => "5-hour"
    };

    public string UsageHistoryRemaining => Language switch
    {
        UiLanguage.TraditionalChinese => "剩餘",
        UiLanguage.SimplifiedChinese => "剩余",
        _ => "Remaining"
    };

    public string UsageHistoryReset => Language switch
    {
        UiLanguage.TraditionalChinese => "重置",
        UiLanguage.SimplifiedChinese => "重置",
        _ => "Reset"
    };

    public string UsageHistoryNoChartData => Language switch
    {
        UiLanguage.TraditionalChinese => "此篩選條件沒有可繪製的資料。",
        UiLanguage.SimplifiedChinese => "此筛选条件没有可绘制的数据。",
        _ => "No chart data for this selection."
    };

    public string UsageHistoryTimestamp => Language switch
    {
        UiLanguage.TraditionalChinese => "時間",
        UiLanguage.SimplifiedChinese => "时间",
        _ => "Timestamp"
    };

    public string UsageHistoryProvider => Language switch
    {
        UiLanguage.TraditionalChinese => "來源",
        UiLanguage.SimplifiedChinese => "来源",
        _ => "Provider"
    };

    public string UsageHistoryFiveHour => "5hr";

    public string UsageHistoryWeek => Language switch
    {
        UiLanguage.TraditionalChinese => "每週",
        UiLanguage.SimplifiedChinese => "每周",
        _ => "Week"
    };

    public string UsageHistoryError => Language switch
    {
        UiLanguage.TraditionalChinese => "錯誤",
        UiLanguage.SimplifiedChinese => "错误",
        _ => "Error"
    };

    public string UsageHistoryEmpty => Language switch
    {
        UiLanguage.TraditionalChinese => "尚無使用記錄。請在設定中啟用 CSV 用量記錄，下一次更新後便會顯示在這裡。",
        UiLanguage.SimplifiedChinese => "暂无使用记录。请在设置中启用 CSV 用量日志，下次刷新后会显示在这里。",
        _ => "No usage history yet. Enable CSV usage logging in Settings; records will appear after the next refresh."
    };

    public string UsageHistoryEmptySubtitle => Language switch
    {
        UiLanguage.TraditionalChinese => "本機 CSV 記錄",
        UiLanguage.SimplifiedChinese => "本地 CSV 记录",
        _ => "Local CSV history"
    };

    public string UsageHistoryRecentActivity => Language switch
    {
        UiLanguage.TraditionalChinese => "最近記錄",
        UiLanguage.SimplifiedChinese => "最近记录",
        _ => "Recent Activity"
    };

    public string UsageHistoryOpenFullTimeline => Language switch
    {
        UiLanguage.TraditionalChinese => "開啟完整時間軸",
        UiLanguage.SimplifiedChinese => "打开完整时间轴",
        _ => "Open Full Timeline"
    };

    public string UsageAnalysisCopyReport => Language switch
    {
        UiLanguage.TraditionalChinese => "複製報告",
        UiLanguage.SimplifiedChinese => "复制报告",
        _ => "Copy Report"
    };

    public string UsageAnalysisReportCopied => Language switch
    {
        UiLanguage.TraditionalChinese => "報告已複製！",
        UiLanguage.SimplifiedChinese => "报告已复制！",
        _ => "Report Copied!"
    };

    public string UsageAnalysisBurnRatePace => Language switch
    {
        UiLanguage.TraditionalChinese => "消耗速度與步調",
        UiLanguage.SimplifiedChinese => "消耗速度与步调",
        _ => "Burn Rate & Pace"
    };

    public string UsageAnalysisQuotaBalance => Language switch
    {
        UiLanguage.TraditionalChinese => "額度消耗比率",
        UiLanguage.SimplifiedChinese => "额度消耗比率",
        _ => "Quota Balance"
    };

    public string UsageAnalysisResetRecovery => Language switch
    {
        UiLanguage.TraditionalChinese => "額度重置與補充",
        UiLanguage.SimplifiedChinese => "额度重置与补充",
        _ => "Replenishment & Resets"
    };

    public string UsageAnalysisKeyTakeaways => Language switch
    {
        UiLanguage.TraditionalChinese => "關鍵分析摘要與額度變化速率",
        UiLanguage.SimplifiedChinese => "关键分析摘要与额度变化速率",
        _ => "Key analytical takeaways and quota velocity"
    };

    public string UsageAnalysisRunwaySafe => Language switch
    {
        UiLanguage.TraditionalChinese => "安全",
        UiLanguage.SimplifiedChinese => "安全",
        _ => "Safe"
    };

    public string UsageAnalysisRunwayModerate => Language switch
    {
        UiLanguage.TraditionalChinese => "適中",
        UiLanguage.SimplifiedChinese => "适中",
        _ => "Moderate"
    };

    public string UsageAnalysisRunwayUrgent => Language switch
    {
        UiLanguage.TraditionalChinese => "吃緊",
        UiLanguage.SimplifiedChinese => "吃紧",
        _ => "Urgent"
    };

    public string UsageAnalysisRunwayStable => Language switch
    {
        UiLanguage.TraditionalChinese => "穩定",
        UiLanguage.SimplifiedChinese => "稳定",
        _ => "Stable"
    };

    public string UsageAnalysisRunwayExhausted => Language switch
    {
        UiLanguage.TraditionalChinese => "已耗盡",
        UiLanguage.SimplifiedChinese => "已耗尽",
        _ => "Exhausted"
    };

    public string UsageAnalysisRunwayNoData => Language switch
    {
        UiLanguage.TraditionalChinese => "無消耗數據",
        UiLanguage.SimplifiedChinese => "无消耗数据",
        _ => "No burn data"
    };

    public string UsageAnalysisDeltaUsed => Language switch
    {
        UiLanguage.TraditionalChinese => "已耗",
        UiLanguage.SimplifiedChinese => "已耗",
        _ => "used"
    };

    public string UsageAnalysisDeltaRestored => Language switch
    {
        UiLanguage.TraditionalChinese => "補充",
        UiLanguage.SimplifiedChinese => "补充",
        _ => "restored"
    };

    public string UsageAnalysisDeltaUnchanged => Language switch
    {
        UiLanguage.TraditionalChinese => "無變動",
        UiLanguage.SimplifiedChinese => "无变动",
        _ => "unchanged"
    };

    public string UsageAnalysisCycleReset => Language switch
    {
        UiLanguage.TraditionalChinese => "週期重置",
        UiLanguage.SimplifiedChinese => "周期重置",
        _ => "cycle reset"
    };

    public string UsageAnalysisIndefinite => Language switch
    {
        UiLanguage.TraditionalChinese => "無限（每小時 0%）",
        UiLanguage.SimplifiedChinese => "无限（每小时 0%）",
        _ => "Indefinite (0% / hr)"
    };

    public string FormatUsageHistoryCount(int count, int visibleLimit) => Language switch
    {
        UiLanguage.TraditionalChinese =>
            count > visibleLimit ? $"本機 CSV 記錄 · 顯示最新 {visibleLimit:N0} / {count:N0} 筆" : $"本機 CSV 記錄 · {count:N0} 筆",
        UiLanguage.SimplifiedChinese =>
            count > visibleLimit ? $"本地 CSV 记录 · 显示最新 {visibleLimit:N0} / {count:N0} 条" : $"本地 CSV 记录 · {count:N0} 条",
        _ => count > visibleLimit
            ? $"Local CSV history · newest {visibleLimit:N0} of {count:N0} records"
            : $"Local CSV history · {count:N0} records"
    };

    public string FormatUsageHistoryReset(DateTimeOffset resetAt) => Language switch
    {
        UiLanguage.TraditionalChinese => $"重置 {resetAt:MMM d HH:mm}",
        UiLanguage.SimplifiedChinese => $"重置 {resetAt:MMM d HH:mm}",
        _ => $"Resets {resetAt:MMM d, HH:mm}"
    };

    public string AntigravityObservedFallback => Language switch
    {
        UiLanguage.TraditionalChinese => "觀察到的模型剩餘用量；共用額度尚未提供",
        UiLanguage.SimplifiedChinese => "观察到的模型剩余用量；共享额度尚未提供",
        _ => "Observed remaining model data; shared quota limits are unavailable"
    };

    public string AntigravityStaleShort => Language switch
    {
        UiLanguage.TraditionalChinese => "已過期",
        UiLanguage.SimplifiedChinese => "已过期",
        _ => "Stale"
    };

    public string ResetPassedAwaitingRefresh => Language switch
    {
        UiLanguage.TraditionalChinese => "已過重置時間・等待新資料",
        UiLanguage.SimplifiedChinese => "已过重置时间・等待新数据",
        _ => "Reset time passed · waiting for new data"
    };

    public string FormatStaleSince(DateTimeOffset? observedAt)
    {
        if (observedAt is not { } value)
        {
            return Language switch
            {
                UiLanguage.TraditionalChinese => "顯示的是上次成功讀取的資料",
                UiLanguage.SimplifiedChinese => "显示的是上次成功读取的数据",
                _ => "Showing the last successful reading"
            };
        }

        var dateTime = FormatDateTime(value, LastUpdatedDateTimeFormat);
        return Language switch
        {
            UiLanguage.TraditionalChinese => $"顯示 {dateTime} 的資料，可能已過期",
            UiLanguage.SimplifiedChinese => $"显示 {dateTime} 的数据，可能已过期",
            _ => $"Showing data from {dateTime}; it may be out of date"
        };
    }

    public string QuotaFiveHourLabel => Language switch
    {
        UiLanguage.TraditionalChinese => "5 小時",
        UiLanguage.SimplifiedChinese => "5 小时",
        _ => "5-hour"
    };

    public string QuotaWeeklyLabel => Language switch
    {
        UiLanguage.TraditionalChinese => "每週",
        UiLanguage.SimplifiedChinese => "每周",
        _ => "Weekly"
    };

    public string RemainingCaption => Language switch
    {
        UiLanguage.TraditionalChinese => "剩餘",
        UiLanguage.SimplifiedChinese => "剩余",
        _ => "left"
    };

    // A window that is still full and has no reset time has not started yet:
    // the provider only starts the clock on the first request.
    public string ResetNotStarted => Language switch
    {
        UiLanguage.TraditionalChinese => "下次使用時開始計算",
        UiLanguage.SimplifiedChinese => "下次使用时开始计算",
        _ => "Starts on next use"
    };

    public string FormatResetsIn(DateTimeOffset localReset, DateTimeOffset now)
    {
        var countdown = FormatShortCountdown(localReset, now);
        return Language switch
        {
            UiLanguage.TraditionalChinese => $"{countdown}後重置",
            UiLanguage.SimplifiedChinese => $"{countdown}后重置",
            _ => $"Resets in {countdown}"
        };
    }

    // The reset moment alone, in the user's chosen date format, for the small
    // detail next to the countdown. A date-only format says nothing about a
    // reset later today, so those show the clock time instead.
    public string FormatResetMoment(DateTimeOffset localReset, DateTimeOffset now)
    {
        var dateOnly = ResetDateTimeFormat is
            DateTimeFormatOptions.MonthDay or DateTimeFormatOptions.YearMonthDay;
        return dateOnly && localReset - now < TimeSpan.FromHours(24)
            ? FormatDateTime(localReset, DateTimeFormatOptions.HourMinute)
            : FormatDateTime(localReset, ResetDateTimeFormat);
    }

    // ---- Settings window -------------------------------------------------

    public string TrayTaskbarGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "系統匣與工作列",
        UiLanguage.SimplifiedChinese => "系统托盘与任务栏",
        _ => "System tray & taskbar"
    };

    public string TrayTaskbarGroupDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "除浮動視窗外，應用程式顯示的位置。",
        UiLanguage.SimplifiedChinese => "除浮动窗口外，应用程序显示的位置。",
        _ => "Where the companion appears besides its overlay."
    };

    public string StartupBehaviorGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "啟動與行為",
        UiLanguage.SimplifiedChinese => "启动与行为",
        _ => "Startup & behavior"
    };

    public string StartupBehaviorGroupDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "應用程式如何啟動並在桌面上運作。",
        UiLanguage.SimplifiedChinese => "应用程序如何启动并在桌面上运行。",
        _ => "How the app launches and behaves on your desktop."
    };

    public string ProviderTagClaude => Language switch
    {
        UiLanguage.TraditionalChinese => "5 小時 · 每週",
        UiLanguage.SimplifiedChinese => "5 小时 · 每周",
        _ => "5-hour · Weekly"
    };

    public string ProviderTagCodex => Language switch
    {
        UiLanguage.TraditionalChinese => "5 小時 · 每週",
        UiLanguage.SimplifiedChinese => "5 小时 · 每周",
        _ => "5-hour · Weekly"
    };

    public string ProviderTagAntigravity => Language switch
    {
        UiLanguage.TraditionalChinese => "Gemini · Claude 與 GPT 額度池",
        UiLanguage.SimplifiedChinese => "Gemini · Claude 与 GPT 额度池",
        _ => "Gemini · Claude & GPT pools"
    };

    public string RefreshGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "更新",
        UiLanguage.SimplifiedChinese => "刷新",
        _ => "Refresh"
    };

    public string LanguageThemeGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "語言與主題",
        UiLanguage.SimplifiedChinese => "语言与主题",
        _ => "Language & theme"
    };

    public string LanguageThemeGroupDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "選擇介面語言與色彩主題。",
        UiLanguage.SimplifiedChinese => "选择界面语言与颜色主题。",
        _ => "Choose the interface language and colour theme."
    };

    public string OverlayPlacementGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "浮動視窗位置",
        UiLanguage.SimplifiedChinese => "浮动窗口位置",
        _ => "Overlay placement"
    };

    public string OverlayPlacementGroupDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "浮動視窗在螢幕上的位置與外觀。",
        UiLanguage.SimplifiedChinese => "浮动窗口在屏幕上的位置与外观。",
        _ => "Where the overlay sits on screen and how it looks."
    };

    public string PositionHint => Language switch
    {
        UiLanguage.TraditionalChinese => "在此選擇位置，或點選下方的螢幕示意圖。",
        UiLanguage.SimplifiedChinese => "在此选择位置，或点击下方的屏幕示意图。",
        _ => "Pick a position here, or click the screen below."
    };

    public string OpacityOption => Language switch
    {
        UiLanguage.TraditionalChinese => "視窗不透明度",
        UiLanguage.SimplifiedChinese => "窗口不透明度",
        _ => "Overlay opacity"
    };

    public string OpacityDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "數值越低，越能透出桌面。",
        UiLanguage.SimplifiedChinese => "数值越低，越能透出桌面。",
        _ => "Lower values let the desktop show through."
    };

    public string MarginOption => Language switch
    {
        UiLanguage.TraditionalChinese => "螢幕邊距",
        UiLanguage.SimplifiedChinese => "屏幕边距",
        _ => "Screen margin"
    };

    public string MarginDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "與螢幕邊緣的距離（像素）。",
        UiLanguage.SimplifiedChinese => "与屏幕边缘的距离（像素）。",
        _ => "Distance from the screen edge, in pixels."
    };

    public string NotificationAlertsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "提醒",
        UiLanguage.SimplifiedChinese => "提醒",
        _ => "Alerts"
    };

    public string NotificationAlertsGroupDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "額度警告與重置通知。",
        UiLanguage.SimplifiedChinese => "额度警告与重置通知。",
        _ => "Quota warnings and reset notices."
    };

    public string TestNotificationOption => Language switch
    {
        UiLanguage.TraditionalChinese => "測試通知",
        UiLanguage.SimplifiedChinese => "测试通知",
        _ => "Test notification"
    };

    public string TestNotificationDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "傳送範例通知，確認桌面是否會顯示。",
        UiLanguage.SimplifiedChinese => "发送示例通知，确认桌面是否会显示。",
        _ => "Send a sample notification to check that your desktop shows it."
    };

    public string TestNotificationAction => Language switch
    {
        UiLanguage.TraditionalChinese => "傳送測試",
        UiLanguage.SimplifiedChinese => "发送测试",
        _ => "Send test"
    };

    public string TestNotificationTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "用量小幫手",
        UiLanguage.SimplifiedChinese => "用量助手",
        _ => "Usage Companion"
    };

    public string TestNotificationBody => Language switch
    {
        UiLanguage.TraditionalChinese => "通知運作正常。",
        UiLanguage.SimplifiedChinese => "通知运行正常。",
        _ => "Notifications are working."
    };

    public string ResetFormatGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "重置時間",
        UiLanguage.SimplifiedChinese => "重置时间",
        _ => "Reset time"
    };

    public string ResetFormatGroupDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "額度重置時間的顯示方式。",
        UiLanguage.SimplifiedChinese => "额度重置时间的显示方式。",
        _ => "How a quota's reset time is written."
    };

    public string LastUpdatedFormatGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "最後更新",
        UiLanguage.SimplifiedChinese => "最后更新",
        _ => "Last updated"
    };

    public string LastUpdatedFormatGroupDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "最近一次更新時間的顯示方式。",
        UiLanguage.SimplifiedChinese => "最近一次刷新时间的显示方式。",
        _ => "How the time of the latest refresh is written."
    };

    public string FormatPreviewLabel => Language switch
    {
        UiLanguage.TraditionalChinese => "預覽",
        UiLanguage.SimplifiedChinese => "预览",
        _ => "Preview"
    };

    public string HistoryFileGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "歷史記錄檔",
        UiLanguage.SimplifiedChinese => "历史记录文件",
        _ => "History file"
    };

    public string HistoryFileGroupDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "額度樣本的寫入位置與保留時間。",
        UiLanguage.SimplifiedChinese => "额度样本的写入位置与保留时间。",
        _ => "Where quota samples are written and how long they are kept."
    };

    public string OpenFolderAction => Language switch
    {
        UiLanguage.TraditionalChinese => "開啟資料夾",
        UiLanguage.SimplifiedChinese => "打开文件夹",
        _ => "Open folder"
    };

    public string ClearHistoryAction => Language switch
    {
        UiLanguage.TraditionalChinese => "清除歷史…",
        UiLanguage.SimplifiedChinese => "清除历史…",
        _ => "Clear history…"
    };

    public string ClearHistoryConfirmTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "清除用量歷史？",
        UiLanguage.SimplifiedChinese => "清除用量历史？",
        _ => "Clear usage history?"
    };

    public string ClearHistoryConfirmAction => Language switch
    {
        UiLanguage.TraditionalChinese => "清除",
        UiLanguage.SimplifiedChinese => "清除",
        _ => "Clear"
    };

    public string NoHistoryFileYet => Language switch
    {
        UiLanguage.TraditionalChinese => "尚未記錄任何歷史",
        UiLanguage.SimplifiedChinese => "尚未记录任何历史",
        _ => "No history recorded yet"
    };

    public string AboutDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "在同一處追蹤 Claude、Codex 與 Antigravity 額度的桌面小幫手。",
        UiLanguage.SimplifiedChinese => "在同一处跟踪 Claude、Codex 和 Antigravity 额度的桌面助手。",
        _ => "Desktop companion that tracks Claude, Codex and Antigravity quota in one place."
    };

    public string ProjectPageAction => Language switch
    {
        UiLanguage.TraditionalChinese => "專案頁面",
        UiLanguage.SimplifiedChinese => "项目页面",
        _ => "Project page"
    };

    public string ReportIssueAction => Language switch
    {
        UiLanguage.TraditionalChinese => "回報問題",
        UiLanguage.SimplifiedChinese => "报告问题",
        _ => "Report an issue"
    };

    public string LicenseAction => Language switch
    {
        UiLanguage.TraditionalChinese => "授權條款",
        UiLanguage.SimplifiedChinese => "许可证",
        _ => "License"
    };

    public string ChangelogAction => Language switch
    {
        UiLanguage.TraditionalChinese => "更新內容",
        UiLanguage.SimplifiedChinese => "更新内容",
        _ => "What's new"
    };

    public string OriginalProjectAction => Language switch
    {
        UiLanguage.TraditionalChinese => "原始專案",
        UiLanguage.SimplifiedChinese => "原始项目",
        _ => "Original project"
    };

    public string CopyDiagnosticsAction => Language switch
    {
        UiLanguage.TraditionalChinese => "複製診斷資訊",
        UiLanguage.SimplifiedChinese => "复制诊断信息",
        _ => "Copy diagnostics"
    };

    public string CopiedNotice => Language switch
    {
        UiLanguage.TraditionalChinese => "已複製",
        UiLanguage.SimplifiedChinese => "已复制",
        _ => "Copied"
    };

    public string DiagnosticsDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "供回報問題使用的版本、系統與設定摘要，不含任何憑證。",
        UiLanguage.SimplifiedChinese => "供报告问题使用的版本、系统与设置摘要，不含任何凭据。",
        _ => "Version, system and settings summary for bug reports. It contains no credentials."
    };

    public string NoSettingsMatch => Language switch
    {
        UiLanguage.TraditionalChinese => "找不到符合的設定。",
        UiLanguage.SimplifiedChinese => "找不到匹配的设置。",
        _ => "No settings match your search."
    };

    public string SearchResultsHeading => Language switch
    {
        UiLanguage.TraditionalChinese => "搜尋結果",
        UiLanguage.SimplifiedChinese => "搜索结果",
        _ => "Search results"
    };

    public string FormatWindowPosition(string position) => WindowPosition.Normalize(position) switch
    {
        WindowPosition.LeftTop => Language switch
        {
            UiLanguage.TraditionalChinese => "左上",
            UiLanguage.SimplifiedChinese => "左上",
            _ => "Top left"
        },
        WindowPosition.MiddleTop => Language switch
        {
            UiLanguage.TraditionalChinese => "上方置中",
            UiLanguage.SimplifiedChinese => "顶部居中",
            _ => "Top center"
        },
        WindowPosition.RightTop => Language switch
        {
            UiLanguage.TraditionalChinese => "右上",
            UiLanguage.SimplifiedChinese => "右上",
            _ => "Top right"
        },
        WindowPosition.LeftCenter => Language switch
        {
            UiLanguage.TraditionalChinese => "左側置中",
            UiLanguage.SimplifiedChinese => "左侧居中",
            _ => "Middle left"
        },
        WindowPosition.MiddleCenter => Language switch
        {
            UiLanguage.TraditionalChinese => "正中央",
            UiLanguage.SimplifiedChinese => "正中",
            _ => "Center"
        },
        WindowPosition.RightCenter => Language switch
        {
            UiLanguage.TraditionalChinese => "右側置中",
            UiLanguage.SimplifiedChinese => "右侧居中",
            _ => "Middle right"
        },
        WindowPosition.LeftBottom => Language switch
        {
            UiLanguage.TraditionalChinese => "左下",
            UiLanguage.SimplifiedChinese => "左下",
            _ => "Bottom left"
        },
        WindowPosition.MiddleBottom => Language switch
        {
            UiLanguage.TraditionalChinese => "下方置中",
            UiLanguage.SimplifiedChinese => "底部居中",
            _ => "Bottom center"
        },
        _ => Language switch
        {
            UiLanguage.TraditionalChinese => "右下",
            UiLanguage.SimplifiedChinese => "右下",
            _ => "Bottom right"
        }
    };

    /// <summary>The given moment written in a candidate date format, for live previews.</summary>
    public string PreviewDateTime(string format, DateTimeOffset value) =>
        FormatDateTime(value, format);

    public string FormatHistoryFileInfo(long bytes, int records)
    {
        var size = bytes >= 1_048_576
            ? $"{bytes / 1_048_576d:0.0} MB"
            : bytes >= 1024 ? $"{bytes / 1024d:0.#} KB" : $"{bytes} B";
        return Language switch
        {
            UiLanguage.TraditionalChinese => $"{size} · {records:N0} 筆記錄",
            UiLanguage.SimplifiedChinese => $"{size} · {records:N0} 条记录",
            _ => $"{size} · {records:N0} records"
        };
    }

    public string FormatClearHistoryConfirm(string path, string info) => Language switch
    {
        UiLanguage.TraditionalChinese => $"這會永久刪除 {path}（{info}），並從零開始重新記錄。",
        UiLanguage.SimplifiedChinese => $"这会永久删除 {path}（{info}），并从零开始重新记录。",
        _ => $"This permanently deletes {path} ({info}). Recording starts again from scratch."
    };

    public string NotifySendMissingNotice => Language switch
    {
        UiLanguage.TraditionalChinese => "找不到 notify-send，因此不會顯示桌面通知。請安裝 libnotify-bin（或發行版對應的套件）。",
        UiLanguage.SimplifiedChinese => "找不到 notify-send，因此不会显示桌面通知。请安装 libnotify-bin（或发行版对应的软件包）。",
        _ => "notify-send was not found, so desktop notifications cannot be shown. Install libnotify-bin (or your distribution's equivalent)."
    };

    public string TestNotificationSentNotice => Language switch
    {
        UiLanguage.TraditionalChinese => "已傳送",
        UiLanguage.SimplifiedChinese => "已发送",
        _ => "Sent"
    };

    public string DiagnosticsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "診斷資訊",
        UiLanguage.SimplifiedChinese => "诊断信息",
        _ => "Diagnostics"
    };

    public string InsightsTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "用量洞察",
        UiLanguage.SimplifiedChinese => "用量洞察",
        _ => "Usage insights"
    };

    public string InsightsTabOverview => Language switch
    {
        UiLanguage.TraditionalChinese => "總覽",
        UiLanguage.SimplifiedChinese => "总览",
        _ => "Overview"
    };

    public string InsightsTabAnalysis => Language switch
    {
        UiLanguage.TraditionalChinese => "分析",
        UiLanguage.SimplifiedChinese => "分析",
        _ => "Analysis"
    };

    public string InsightsTabEfficiency => Language switch
    {
        UiLanguage.TraditionalChinese => "效率",
        UiLanguage.SimplifiedChinese => "效率",
        _ => "Efficiency"
    };

    public string InsightsTabRecords => Language switch
    {
        UiLanguage.TraditionalChinese => "記錄",
        UiLanguage.SimplifiedChinese => "记录",
        _ => "Records"
    };

    public string InsightsViewAllRecords => Language switch
    {
        UiLanguage.TraditionalChinese => "檢視所有記錄",
        UiLanguage.SimplifiedChinese => "查看所有记录",
        _ => "View all records"
    };

    public string InsightsWindowShortcutsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "用量洞察",
        UiLanguage.SimplifiedChinese => "用量洞察",
        _ => "Usage insights"
    };

    public string SwitchTabShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "切換分頁",
        UiLanguage.SimplifiedChinese => "切换标签页",
        _ => "Switch tab"
    };

    public string NextTabShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "下一個分頁",
        UiLanguage.SimplifiedChinese => "下一个标签页",
        _ => "Next tab"
    };

    public string SearchRecordsShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "搜尋記錄",
        UiLanguage.SimplifiedChinese => "搜索记录",
        _ => "Search records"
    };

    public string SearchSettingsShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "搜尋設定",
        UiLanguage.SimplifiedChinese => "搜索设置",
        _ => "Search settings"
    };

    public string OpenInsightsShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "開啟用量洞察",
        UiLanguage.SimplifiedChinese => "打开用量洞察",
        _ => "Open usage insights"
    };

    public string ShortcutsCloseHint => Language switch
    {
        UiLanguage.TraditionalChinese => "按 Esc 或 F1 關閉",
        UiLanguage.SimplifiedChinese => "按 Esc 或 F1 关闭",
        _ => "Press Esc or F1 to close"
    };

    public string PreviousPeriodShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "上一個期間",
        UiLanguage.SimplifiedChinese => "上一个期间",
        _ => "Previous period"
    };

    public string NextPeriodShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "下一個期間",
        UiLanguage.SimplifiedChinese => "下一个期间",
        _ => "Next period"
    };

    public string JumpToTodayShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "跳到今天",
        UiLanguage.SimplifiedChinese => "跳到今天",
        _ => "Jump to today"
    };

    public string EnableCodexUsageOption => Language switch
    {
        UiLanguage.TraditionalChinese => "啟用 Codex 用量顯示",
        UiLanguage.SimplifiedChinese => "启用 Codex 用量显示",
        _ => "Enable Codex usage"
    };

    public string EnableAntigravityUsageOption => Language switch
    {
        UiLanguage.TraditionalChinese => "啟用 Antigravity 用量顯示",
        UiLanguage.SimplifiedChinese => "启用 Antigravity 用量显示",
        _ => "Enable Antigravity usage"
    };

    public string EnableClaudeUsageOption => Language switch
    {
        UiLanguage.TraditionalChinese => "啟用 Claude 用量顯示",
        UiLanguage.SimplifiedChinese => "启用 Claude 用量显示",
        _ => "Enable Claude usage"
    };

    public string DisplayedLimitsOption => Language switch
    {
        UiLanguage.TraditionalChinese => "顯示的用量限制",
        UiLanguage.SimplifiedChinese => "显示的用量限制",
        _ => "Displayed limits"
    };

    public string ShowClaudeSessionOption => Language switch
    {
        UiLanguage.TraditionalChinese => "Claude 目前工作階段",
        UiLanguage.SimplifiedChinese => "Claude 当前会话",
        _ => "Claude current session"
    };

    public string ShowClaudeWeeklyOption => Language switch
    {
        UiLanguage.TraditionalChinese => "Claude 本週用量（全部）",
        UiLanguage.SimplifiedChinese => "Claude 本周用量（全部）",
        _ => "Claude current week (All)"
    };

    public string ShowCodexFiveHourOption => Language switch
    {
        UiLanguage.TraditionalChinese => "Codex 5 小時用量限制",
        UiLanguage.SimplifiedChinese => "Codex 5 小时用量限制",
        _ => "Codex 5-hour limit"
    };

    public string ShowCodexWeeklyOption => Language switch
    {
        UiLanguage.TraditionalChinese => "Codex 每週用量限制",
        UiLanguage.SimplifiedChinese => "Codex 每周用量限制",
        _ => "Codex weekly limit"
    };

    public string MinimizeOnStartOption => Language switch
    {
        UiLanguage.TraditionalChinese => "最小化視窗",
        UiLanguage.SimplifiedChinese => "最小化窗口",
        _ => "Minimize the window"
    };

    public string ShowTaskbarIconOption => Language switch
    {
        UiLanguage.TraditionalChinese => "在工作列顯示圖示",
        UiLanguage.SimplifiedChinese => "在任务栏显示图标",
        _ => "Show icon in the taskbar"
    };

    public string TrayIconStyleOption => Language switch
    {
        UiLanguage.TraditionalChinese => "系統匣圖示樣式",
        UiLanguage.SimplifiedChinese => "系统托盘图标样式",
        _ => "System tray icon style"
    };

    public string OriginalTrayIconStyle => Language switch
    {
        UiLanguage.TraditionalChinese => "原始圖示",
        UiLanguage.SimplifiedChinese => "原始图标",
        _ => "Original icon"
    };

    public string ClaudeCurrentTrayIconStyle => Language switch
    {
        UiLanguage.TraditionalChinese => "Claude 目前工作階段剩餘用量",
        UiLanguage.SimplifiedChinese => "Claude 当前会话剩余用量",
        _ => "Claude current session remaining"
    };

    public string ClaudeWeeklyTrayIconStyle => Language switch
    {
        UiLanguage.TraditionalChinese => "Claude 每週工作階段剩餘用量",
        UiLanguage.SimplifiedChinese => "Claude 每周会话剩余用量",
        _ => "Claude weekly session remaining"
    };

    public string CodexSessionTrayIconStyle => Language switch
    {
        UiLanguage.TraditionalChinese => "Codex 工作階段剩餘用量",
        UiLanguage.SimplifiedChinese => "Codex 会话剩余用量",
        _ => "Codex session remaining"
    };

    public string OkAction => Language switch
    {
        UiLanguage.TraditionalChinese => "確定",
        UiLanguage.SimplifiedChinese => "确定",
        _ => "OK"
    };

    public string ApplyAction => Language switch
    {
        UiLanguage.TraditionalChinese => "應用",
        UiLanguage.SimplifiedChinese => "应用",
        _ => "Apply"
    };

    public string WindowSettingsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "視窗與啟動",
        UiLanguage.SimplifiedChinese => "窗口与启动",
        _ => "Window and startup"
    };

    public string UsageSettingsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "用量",
        UiLanguage.SimplifiedChinese => "用量",
        _ => "Usage"
    };

    public string AppearanceSettingsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "外觀",
        UiLanguage.SimplifiedChinese => "外观",
        _ => "Appearance"
    };

    public string NotificationSettingsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "通知",
        UiLanguage.SimplifiedChinese => "通知",
        _ => "Notifications"
    };

    public string DateTimeSettingsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "日期與時間",
        UiLanguage.SimplifiedChinese => "日期与时间",
        _ => "Date and time"
    };

    public string LoggingSettingsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "記錄",
        UiLanguage.SimplifiedChinese => "日志",
        _ => "Logging"
    };

    public string AboutSettingsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "關於",
        UiLanguage.SimplifiedChinese => "关于",
        _ => "About"
    };

    public string SearchSettingsPlaceholder => Language switch
    {
        UiLanguage.TraditionalChinese => "搜尋設定...",
        UiLanguage.SimplifiedChinese => "搜索设置...",
        _ => "Search settings..."
    };

    public string UnsavedChangesNotice => Language switch
    {
        UiLanguage.TraditionalChinese => "有未儲存的變更",
        UiLanguage.SimplifiedChinese => "有未保存的更改",
        _ => "Unsaved changes"
    };

    public string WindowSettingsDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "設定視窗行為、系統匣與開機啟動偏好",
        UiLanguage.SimplifiedChinese => "配置窗口行为、系统托盘和启动偏好",
        _ => "Configure window behavior, system tray, and startup preferences"
    };

    public string UsageSettingsDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "啟用 AI 提供者並自訂顯示的用量上限",
        UiLanguage.SimplifiedChinese => "启用 AI 提供商并自定义显示的用量上限",
        _ => "Enable AI providers and customize displayed rate limits"
    };

    public string AppearanceSettingsDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "自訂語言、佈景主題與桌面覆蓋視窗位置",
        UiLanguage.SimplifiedChinese => "自定义语言、主题与桌面覆盖窗口位置",
        _ => "Customize language, theme, and desktop overlay placement"
    };

    public string NotificationSettingsDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "設定額度警示與重置通知",
        UiLanguage.SimplifiedChinese => "设置配额预警和重置通知",
        _ => "Configure quota warnings and reset notifications"
    };

    public string DateTimeSettingsDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "自訂隨伴視窗中的日期與時間格式",
        UiLanguage.SimplifiedChinese => "自定义伴侣窗口中的日期与时间格式",
        _ => "Customize date and time formatting across the companion"
    };

    public string LoggingSettingsDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "設定本機 CSV 或 JSONL 用量歷程記錄",
        UiLanguage.SimplifiedChinese => "设置本地 CSV 或 JSONL 用量历史记录",
        _ => "Configure local CSV or JSONL usage history logging"
    };

    public string AboutSettingsDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "應用程式詳細資訊與版本資訊",
        UiLanguage.SimplifiedChinese => "应用程序详细信息与版本信息",
        _ => "Application details and version information"
    };

    public string SystemTrayDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "將隨伴視窗縮小至系統匣通知區域",
        UiLanguage.SimplifiedChinese => "将伴侣窗口最小化到系统托盘通知区域",
        _ => "Minimize companion to the system tray notification area"
    };

    public string TrayIconStyleDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "選擇在系統匣中顯示動態額度百分比或預設圖示",
        UiLanguage.SimplifiedChinese => "选择在系统托盘中显示动态配额百分比或默认图标",
        _ => "Choose dynamic quota percentage or default icon in the system tray"
    };

    public string ShowTaskbarIconDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "在工作列或 Dock 中顯示應用程式圖示",
        UiLanguage.SimplifiedChinese => "在任务栏或 Dock 中显示应用程序图标",
        _ => "Show app icon in the taskbar or dock"
    };

    public string StartOnBootDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "在開機登入系統時自動啟動隨伴視窗",
        UiLanguage.SimplifiedChinese => "在开机登录系统时自动启动伴侣窗口",
        _ => "Automatically launch companion on system startup"
    };

    public string MinimizeOnStartDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "開機啟動時自動縮小至系統匣，不彈出視窗",
        UiLanguage.SimplifiedChinese => "开机启动时自动隐藏到系统托盘，不弹出窗口",
        _ => "Hide main window to tray immediately on system launch"
    };

    public string AlwaysOnTopDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "保持桌面隨伴視窗浮動於其他視窗最上層",
        UiLanguage.SimplifiedChinese => "保持桌面伴侣窗口浮动在其他窗口最上层",
        _ => "Keep companion overlay floating above other desktop windows"
    };

    public string ClaudeProviderDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "追蹤 Anthropic Claude 會話與每週用量額度",
        UiLanguage.SimplifiedChinese => "跟踪 Anthropic Claude 会话与每周用量配额",
        _ => "Track Anthropic Claude session and weekly usage limits"
    };

    public string ClaudeSessionLimitDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "顯示 5 小時目前會話額度進度條與重置倒數",
        UiLanguage.SimplifiedChinese => "显示 5 小时当前会话配额进度条与重置倒数",
        _ => "Show 5-hour current session quota meter and reset countdown"
    };

    public string ClaudeWeeklyLimitDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "顯示 7 天滑動週期用量額度進度條",
        UiLanguage.SimplifiedChinese => "显示 7 天滑动周期用量配额进度条",
        _ => "Show 7-day rolling window quota meter"
    };

    public string CodexProviderDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "追蹤 OpenAI Codex 速率限制與請求額度",
        UiLanguage.SimplifiedChinese => "跟踪 OpenAI Codex 速率限制与请求配额",
        _ => "Track OpenAI Codex rate limits and request allowances"
    };

    public string CodexFiveHourLimitDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "顯示 5 小時主要速率限制額度進度條",
        UiLanguage.SimplifiedChinese => "显示 5 小时主要速率限制配额进度条",
        _ => "Show 5-hour primary rate limit quota meter"
    };

    public string CodexWeeklyLimitDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "顯示每週滑動速率限制額度進度條",
        UiLanguage.SimplifiedChinese => "显示每周滑动速率限制配额进度条",
        _ => "Show weekly rolling rate limit quota meter"
    };

    public string AntigravityProviderDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "追蹤 Google DeepMind Antigravity 額度與配額容量",
        UiLanguage.SimplifiedChinese => "跟踪 Google DeepMind Antigravity 配额与配额容量",
        _ => "Track Google DeepMind Antigravity quota and tier capacity"
    };

    public string RefreshIntervalDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "從 API 輪詢抓取最新用量資料的間隔頻率",
        UiLanguage.SimplifiedChinese => "从 API 轮询获取最新用量数据的间隔频率",
        _ => "Polling frequency for fetching fresh usage stats from APIs"
    };

    public string SystemLanguage => Language switch
    {
        UiLanguage.TraditionalChinese => "依系統語言",
        UiLanguage.SimplifiedChinese => "跟随系统语言",
        _ => "Follow system language"
    };

    public string LanguageOptionDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "選擇使用者介面顯示語言",
        UiLanguage.SimplifiedChinese => "选择用户界面显示语言",
        _ => "Select user interface display language"
    };

    public string ThemeOptionDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "選擇深色、淺色或跟隨系統外觀設定",
        UiLanguage.SimplifiedChinese => "选择深色、浅色或跟随系统外观设置",
        _ => "Choose Dark, Light, or follow system theme appearance"
    };

    public string PositionOptionDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "桌面覆蓋視窗貼齊的螢幕角落或邊緣位置",
        UiLanguage.SimplifiedChinese => "桌面覆盖窗口吸附的屏幕角落或边缘位置",
        _ => "Screen corner or edge docking position for the overlay"
    };

    public string LowUsageAlertDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "當剩餘額度低於警戒門檻時發送桌面通知提醒",
        UiLanguage.SimplifiedChinese => "当剩余配额低于预警阈值时发送桌面通知提醒",
        _ => "Trigger alert notification when remaining quota falls below threshold"
    };

    public string LowUsageThresholdDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "觸發低額度警告的剩餘配額百分比",
        UiLanguage.SimplifiedChinese => "触发低配额警告的剩余配额百分比",
        _ => "Percentage of remaining quota that triggers warning"
    };

    public string NotifyOnResetDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "當額度完全重置並恢復滿額時發送通知",
        UiLanguage.SimplifiedChinese => "当配额完全重置并恢复满额时发送通知",
        _ => "Send notification when rate limits are reset and fully refreshed"
    };

    public string ResetDateTimeFormatDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "速率限制重置時間的日期時間格式",
        UiLanguage.SimplifiedChinese => "速率限制重置时间的日期时间格式",
        _ => "Timestamp format for rate limit reset time"
    };

    public string LastUpdatedDateTimeFormatDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "上次成功更新用量資料的時間格式",
        UiLanguage.SimplifiedChinese => "上次成功更新用量数据的时间格式",
        _ => "Timestamp format for last successful data refresh"
    };

    public string UsageLoggingDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "將帶時間戳記的用量數據記錄至硬碟供分析與圖表使用",
        UiLanguage.SimplifiedChinese => "将带时间戳的用量数据记录到硬盘以供分析与图表使用",
        _ => "Record timestamped quota samples to disk for analytics"
    };

    public string UsageLogFilePathDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "存放用量歷程記錄檔案的目的地路徑",
        UiLanguage.SimplifiedChinese => "存放用量历史记录文件的目标路径",
        _ => "Destination file path for storing usage log entries"
    };

    public string UsageLogFormatDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "歷程記錄檔案的儲存格式（CSV 或 JSONL）",
        UiLanguage.SimplifiedChinese => "历史记录文件的存储格式（CSV 或 JSONL）",
        _ => "Data file format for logging (CSV or JSONL)"
    };

    public string UsageLogRetentionOption => Language switch
    {
        UiLanguage.TraditionalChinese => "歷史保留期限",
        UiLanguage.SimplifiedChinese => "历史保留期限",
        _ => "History retention"
    };

    public string UsageLogRetentionDescription => Language switch
    {
        UiLanguage.TraditionalChinese => "自動移除較舊的記錄，避免檔案無限增長",
        UiLanguage.SimplifiedChinese => "自动移除较旧的记录，避免文件无限增长",
        _ => "Older records are removed automatically so the file does not grow forever"
    };

    public string FormatUsageLogRetention(int days) => days switch
    {
        UsageLogRetentionOptions.Forever => Language switch
        {
            UiLanguage.TraditionalChinese => "全部保留",
            UiLanguage.SimplifiedChinese => "全部保留",
            _ => "Keep everything"
        },
        365 => Language switch
        {
            UiLanguage.TraditionalChinese => "1 年",
            UiLanguage.SimplifiedChinese => "1 年",
            _ => "1 year"
        },
        _ => Language switch
        {
            UiLanguage.TraditionalChinese => $"{days} 天",
            UiLanguage.SimplifiedChinese => $"{days} 天",
            _ => $"{days} days"
        }
    };

    public string BrowseFileAction => Language switch
    {
        UiLanguage.TraditionalChinese => "瀏覽...",
        UiLanguage.SimplifiedChinese => "浏览...",
        _ => "Browse..."
    };

    public string ScreenPositionVisual => Language switch
    {
        UiLanguage.TraditionalChinese => "可視化螢幕位置",
        UiLanguage.SimplifiedChinese => "可视化屏幕位置",
        _ => "Visual Screen Position"
    };

    public string QuickPresetsLabel => Language switch
    {
        UiLanguage.TraditionalChinese => "常用預設",
        UiLanguage.SimplifiedChinese => "常用预设",
        _ => "Presets"
    };

    public string PinOnTopAction => Language switch
    {
        UiLanguage.TraditionalChinese => "將視窗釘選在最上層",
        UiLanguage.SimplifiedChinese => "将窗口固定在最上层",
        _ => "Pin window on top"
    };

    public string UnpinFromTopAction => Language switch
    {
        UiLanguage.TraditionalChinese => "取消視窗置頂",
        UiLanguage.SimplifiedChinese => "取消窗口置顶",
        _ => "Unpin window from top"
    };

    public string ShortcutsAction => Language switch
    {
        UiLanguage.TraditionalChinese => "鍵盤快捷鍵",
        UiLanguage.SimplifiedChinese => "键盘快捷键",
        _ => "Keyboard shortcuts"
    };

    public string ShortcutsTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "鍵盤快捷鍵 - Claude Codex Usage Companion",
        UiLanguage.SimplifiedChinese => "键盘快捷键 - Claude Codex Usage Companion",
        _ => "Keyboard shortcuts - Claude Codex Usage Companion"
    };

    public string MainWindowShortcutsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "主視窗",
        UiLanguage.SimplifiedChinese => "主窗口",
        _ => "Main window"
    };

    public string SettingsWindowShortcutsGroup => Language switch
    {
        UiLanguage.TraditionalChinese => "設定視窗",
        UiLanguage.SimplifiedChinese => "设置窗口",
        _ => "Settings window"
    };

    public string ShowShortcutsShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "顯示鍵盤快捷鍵",
        UiLanguage.SimplifiedChinese => "显示键盘快捷键",
        _ => "Show keyboard shortcuts"
    };

    public string OpenSettingsShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "開啟設定",
        UiLanguage.SimplifiedChinese => "打开设置",
        _ => "Open Settings"
    };

    public string CloseWindowShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "關閉視窗",
        UiLanguage.SimplifiedChinese => "关闭窗口",
        _ => "Close the window"
    };

    public string SaveSettingsShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "儲存設定",
        UiLanguage.SimplifiedChinese => "保存设置",
        _ => "Save settings"
    };

    public string CloseSettingsShortcut => Language switch
    {
        UiLanguage.TraditionalChinese => "關閉設定",
        UiLanguage.SimplifiedChinese => "关闭设置",
        _ => "Close Settings"
    };

    public string CompactModeAction => Language switch
    {
        UiLanguage.TraditionalChinese => "精簡模式",
        UiLanguage.SimplifiedChinese => "精简模式",
        _ => "Compact mode"
    };

    public IReadOnlyList<ShortcutGroup> ShortcutGroups => new[]
    {
        new ShortcutGroup(
            MainWindowShortcutsGroup,
            new[]
            {
                new ShortcutHint("F1", ShowShortcutsShortcut),
                new ShortcutHint("S", OpenSettingsShortcut),
                new ShortcutHint("I", OpenInsightsShortcut),
                new ShortcutHint("Ctrl+R", RefreshAction),
                new ShortcutHint("Ctrl+M", CompactModeAction),
                new ShortcutHint("Esc", CloseWindowShortcut)
            }),
        new ShortcutGroup(
            InsightsWindowShortcutsGroup,
            new[]
            {
                new ShortcutHint("Ctrl+1–4", SwitchTabShortcut),
                new ShortcutHint("Ctrl+Tab", NextTabShortcut),
                new ShortcutHint("Ctrl+F", SearchRecordsShortcut),
                new ShortcutHint("Ctrl+R", RefreshAction),
                new ShortcutHint("Alt+←", PreviousPeriodShortcut),
                new ShortcutHint("Alt+→", NextPeriodShortcut),
                new ShortcutHint("Alt+Home", JumpToTodayShortcut),
                new ShortcutHint("Esc", CloseWindowShortcut)
            }),
        new ShortcutGroup(
            SettingsWindowShortcutsGroup,
            new[]
            {
                new ShortcutHint("Ctrl+F", SearchSettingsShortcut),
                new ShortcutHint("Ctrl+S", SaveSettingsShortcut),
                new ShortcutHint("Esc", CloseSettingsShortcut)
            })
    };

    public string UnsavedChangesTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "尚未儲存變更",
        UiLanguage.SimplifiedChinese => "尚未保存更改",
        _ => "Unsaved changes"
    };

    public string UnsavedChangesMessage => Language switch
    {
        UiLanguage.TraditionalChinese => "設定中有尚未儲存的變更。要捨棄這些變更嗎？",
        UiLanguage.SimplifiedChinese => "设置中有尚未保存的更改。要放弃这些更改吗？",
        _ => "Your settings contain unsaved changes. Discard them?"
    };

    public string DiscardChangesAction => Language switch
    {
        UiLanguage.TraditionalChinese => "捨棄變更",
        UiLanguage.SimplifiedChinese => "放弃更改",
        _ => "Discard changes"
    };

    public string KeepEditingAction => Language switch
    {
        UiLanguage.TraditionalChinese => "繼續編輯",
        UiLanguage.SimplifiedChinese => "继续编辑",
        _ => "Keep editing"
    };

    public string ClaudeFiveHourTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "目前工作階段",
        UiLanguage.SimplifiedChinese => "当前会话",
        _ => "Current session"
    };

    public string ClaudeWeeklyTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "本週用量（全部）",
        UiLanguage.SimplifiedChinese => "本周用量（全部）",
        _ => "Current week (All)"
    };

    public string CombinedUsageHeaderTitle => "USAGE";

    public string ClaudeLowUsageAlertTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "Claude 剩餘用量偏低",
        UiLanguage.SimplifiedChinese => "Claude 剩余用量偏低",
        _ => "Claude usage is running low"
    };

    public string ClaudeUsageResetTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "Claude 用量已重置",
        UiLanguage.SimplifiedChinese => "Claude 用量已重置",
        _ => "Claude usage has reset"
    };

    public string FormatClaudeCreditsDetails(RateLimitExtraUsageState? extraUsage)
    {
        if (extraUsage is null)
        {
            return Language switch
            {
                UiLanguage.TraditionalChinese => "使用點數：--, 自動加購：--",
                UiLanguage.SimplifiedChinese => "使用点数：--, 自动加购：--",
                _ => "Usage credits: --, Auto-reload: --"
            };
        }

        if (!extraUsage.Enabled)
        {
            return Language switch
            {
                UiLanguage.TraditionalChinese => "使用點數：--, 自動加購：已停用",
                UiLanguage.SimplifiedChinese => "使用点数：--, 自动加购：已禁用",
                _ => "Usage credits: --, Auto-reload: Disabled"
            };
        }

        var credits = FormatClaudeCreditsAmount(extraUsage);
        return Language switch
        {
            UiLanguage.TraditionalChinese => $"使用點數：{credits}, 自動加購：已啟用",
            UiLanguage.SimplifiedChinese => $"使用点数：{credits}, 自动加购：已启用",
            _ => $"Usage credits: {credits}, Auto-reload: Enabled"
        };
    }

    private static string FormatClaudeCreditsAmount(RateLimitExtraUsageState extraUsage)
    {
        if (extraUsage.UsedAmount is not decimal used || extraUsage.LimitAmount is not decimal limit)
        {
            return "--";
        }

        var usedText = used.ToString("0.00", CultureInfo.InvariantCulture);
        var limitText = limit.ToString("0.00", CultureInfo.InvariantCulture);
        return extraUsage.Currency switch
        {
            null => $"{usedText} / {limitText}",
            "USD" => $"${usedText} / ${limitText}",
            _ => $"{usedText} / {limitText} {extraUsage.Currency}"
        };
    }

    public string LowUsageAlertOption => Language switch
    {
        UiLanguage.TraditionalChinese => "剩餘用量低於門檻時發出通知",
        UiLanguage.SimplifiedChinese => "剩余用量低于阈值时发出通知",
        _ => "Alert when remaining usage is below the threshold"
    };

    public string LowUsageAlertThresholdOption => Language switch
    {
        UiLanguage.TraditionalChinese => "低用量通知門檻（%）",
        UiLanguage.SimplifiedChinese => "低用量通知阈值（%）",
        _ => "Low-usage alert threshold (%)"
    };

    public string NotifyOnResetOption => Language switch
    {
        UiLanguage.TraditionalChinese => "用量重置時發出通知",
        UiLanguage.SimplifiedChinese => "用量重置时发出通知",
        _ => "Notify when usage resets"
    };

    public string LowUsageAlertTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "Codex 剩餘用量偏低",
        UiLanguage.SimplifiedChinese => "Codex 剩余用量偏低",
        _ => "Codex usage is running low"
    };

    public string UsageResetTitle => Language switch
    {
        UiLanguage.TraditionalChinese => "Codex 用量已重置",
        UiLanguage.SimplifiedChinese => "Codex 用量已重置",
        _ => "Codex usage has reset"
    };

    public string FormatCreditDetails(
        string? creditBalance,
        bool automaticReloadEnabled)
    {
        var balance = FormatCreditBalance(creditBalance);
        return Language switch
        {
            UiLanguage.TraditionalChinese =>
                $"點數：{balance}, 自動儲值：{(automaticReloadEnabled ? "已啟用" : "已停用")}",
            UiLanguage.SimplifiedChinese =>
                $"点数：{balance}, 自动充值：{(automaticReloadEnabled ? "已启用" : "已禁用")}",
            _ =>
                $"Credits: {balance}, Automatic reload: {(automaticReloadEnabled ? "Enabled" : "Disabled")}"
        };
    }

    private static string FormatCreditBalance(string? creditBalance)
    {
        if (string.IsNullOrWhiteSpace(creditBalance))
        {
            return "--";
        }

        return decimal.TryParse(
            creditBalance,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var numericBalance)
            ? numericBalance.ToString("0.00", CultureInfo.InvariantCulture)
            : "--";
    }

    public string FormatLowUsageAlert(bool weekly, int remainingPercent)
    {
        var limit = FormatLimitName(weekly);
        return Language switch
        {
            UiLanguage.TraditionalChinese => $"{limit}剩餘 {remainingPercent}%。",
            UiLanguage.SimplifiedChinese => $"{limit}剩余 {remainingPercent}%。",
            _ => $"{limit} has {remainingPercent}% remaining."
        };
    }

    public string FormatResetNotification(bool weekly, int remainingPercent)
    {
        var limit = FormatLimitName(weekly);
        return Language switch
        {
            UiLanguage.TraditionalChinese =>
                $"{limit}已重置，目前剩餘 {remainingPercent}%。",
            UiLanguage.SimplifiedChinese =>
                $"{limit}已重置，目前剩余 {remainingPercent}%。",
            _ => $"{limit} has reset and is now {remainingPercent}% remaining."
        };
    }

    public string FormatFiveHourReset(DateTimeOffset localReset)
    {
        return FormatResetDateTime(localReset);
    }

    public string FormatWeeklyReset(DateTimeOffset localReset)
    {
        return FormatResetDateTime(localReset);
    }

    public string FormatResetWithCountdown(DateTimeOffset localReset, DateTimeOffset now)
    {
        var minutesRemaining = Math.Max(
            0,
            (int)Math.Ceiling((localReset - now).TotalMinutes));
        var days = minutesRemaining / (24 * 60);
        var hours = (minutesRemaining % (24 * 60)) / 60;
        var minutes = minutesRemaining % 60;
        var countdown = Language switch
        {
            UiLanguage.TraditionalChinese => $"{days}天 {hours}小時 {minutes}分",
            UiLanguage.SimplifiedChinese => $"{days}天 {hours}小时 {minutes}分",
            _ => $"{days}d {hours}h {minutes}m"
        };

        return $"{FormatResetDateTime(localReset)} · {countdown}";
    }

    public string FormatShortCountdown(DateTimeOffset localReset, DateTimeOffset now)
    {
        var minutesRemaining = Math.Max(
            0,
            (int)Math.Ceiling((localReset - now).TotalMinutes));
        var days = minutesRemaining / (24 * 60);
        var hours = (minutesRemaining % (24 * 60)) / 60;
        var minutes = minutesRemaining % 60;
        if (days > 0)
        {
            return Language switch
            {
                UiLanguage.TraditionalChinese => $"{days}天 {hours}時",
                UiLanguage.SimplifiedChinese => $"{days}天 {hours}时",
                _ => $"{days}d {hours}h"
            };
        }
        if (hours > 0)
        {
            return Language switch
            {
                UiLanguage.TraditionalChinese => $"{hours}小時 {minutes}分",
                UiLanguage.SimplifiedChinese => $"{hours}小时 {minutes}分",
                _ => $"{hours}h {minutes}m"
            };
        }
        return Language switch
        {
            UiLanguage.TraditionalChinese => $"{minutes}分",
            UiLanguage.SimplifiedChinese => $"{minutes}分",
            _ => $"{minutes}m"
        };
    }

    public string FormatUpdatedTime(DateTimeOffset updatedAt)
    {
        var dateTime = FormatDateTime(updatedAt, LastUpdatedDateTimeFormat);
        return Language switch
        {
            UiLanguage.TraditionalChinese => $"最後更新於 {dateTime}",
            UiLanguage.SimplifiedChinese => $"最后更新于 {dateTime}",
            _ => $"Last updated at {dateTime}"
        };
    }

    public string FormatUpdateInterval(int seconds)
    {
        var normalizedSeconds = UpdateIntervalOptions.Normalize(seconds);
        var minutes = (normalizedSeconds / 60d).ToString(
            "0.##",
            CultureInfo.InvariantCulture);
        return Language switch
        {
            UiLanguage.TraditionalChinese => $"每 {minutes} 分鐘",
            UiLanguage.SimplifiedChinese => $"每 {minutes} 分钟",
            _ when normalizedSeconds == 60 => "Every 1 minute",
            _ => $"Every {minutes} minutes"
        };
    }

    public string FormatTrayTooltip(
        RateLimitState? codexState,
        DateTimeOffset? codexUpdatedAt,
        RateLimitState? claudeState,
        DateTimeOffset? claudeUpdatedAt)
    {
        var updatedAt = claudeUpdatedAt ?? codexUpdatedAt;
        return string.Join(
            Environment.NewLine,
            "Claude Codex Usage Companion",
            FormatTrayUsageLine("Claude", TrayCurrentLabel, claudeState?.FiveHour),
            FormatTrayUsageLine("Claude", TrayWeeklyLabel, claudeState?.Weekly),
            FormatTrayUsageLine("Codex", TrayWeeklyLabel, codexState?.Weekly),
            updatedAt is null ? WaitingForData : FormatUpdatedTime(updatedAt.Value));
    }

    private string FormatTrayUsageLine(string provider, string label, RateLimitWindowState? window)
    {
        var remaining = window is null ? RemainingUnavailable : FormatRemaining(window.RemainingPercent);
        var reset = window?.ResetsAt is long unixSeconds
            ? FormatWeeklyReset(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime())
            : ResetUnavailable;
        return $"[{provider}] {label}: {remaining} ({reset})";
    }

    private string TrayCurrentLabel => Language switch
    {
        UiLanguage.TraditionalChinese => "目前",
        UiLanguage.SimplifiedChinese => "当前",
        _ => "Current"
    };

    private string TrayWeeklyLabel => Language switch
    {
        UiLanguage.TraditionalChinese => "每週",
        UiLanguage.SimplifiedChinese => "每周",
        _ => "Weekly"
    };

    public bool TryFormatDateTime(
        DateTimeOffset value,
        string? format,
        out string formatted)
    {
        if (!DateTimeFormatOptions.IsValid(format))
        {
            formatted = string.Empty;
            return false;
        }

        try
        {
            formatted = FormatDateTime(value, format!);
            return true;
        }
        catch (FormatException)
        {
            formatted = string.Empty;
            return false;
        }
    }

    private string FormatLimitName(bool weekly)
    {
        if (weekly)
        {
            return WeeklyTitle;
        }

        return FiveHourTitle;
    }

    private string FormatResetDateTime(DateTimeOffset localReset)
    {
        var dateTime = FormatDateTime(localReset, ResetDateTimeFormat);
        return Language switch
        {
            UiLanguage.TraditionalChinese => $"於 {dateTime} 重置",
            UiLanguage.SimplifiedChinese => $"于 {dateTime} 重置",
            _ => $"Resets at {dateTime}"
        };
    }

    private string FormatDateTime(DateTimeOffset value, string format)
    {
        return format switch
        {
            DateTimeFormatOptions.MonthDay => FormatMonthDay(value),
            DateTimeFormatOptions.MonthDayTime =>
                $"{FormatMonthDay(value)} {value.ToString("HH:mm", CultureInfo.InvariantCulture)}",
            DateTimeFormatOptions.YearMonthDay =>
                value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeFormatOptions.YearMonthDayTime =>
                value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            DateTimeFormatOptions.HourMinuteSecond =>
                value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeFormatOptions.HourMinute =>
                value.ToString("HH:mm", CultureInfo.InvariantCulture),
            _ => value.ToString(
                DateTimeFormatOptions.ToDotNetFormat(format),
                LanguageCulture())
        };
    }

    private string FormatMonthDay(DateTimeOffset value)
    {
        return Language == UiLanguage.English
            ? value.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))
            : $"{value.Month}月{value.Day}日";
    }

    private CultureInfo LanguageCulture()
    {
        return Language switch
        {
            UiLanguage.TraditionalChinese => CultureInfo.GetCultureInfo("zh-TW"),
            UiLanguage.SimplifiedChinese => CultureInfo.GetCultureInfo("zh-CN"),
            _ => CultureInfo.GetCultureInfo("en-US")
        };
    }
}
