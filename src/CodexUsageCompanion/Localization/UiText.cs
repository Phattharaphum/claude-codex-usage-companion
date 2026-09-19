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

    public string UsageHistoryHideTimeline => Language switch
    {
        UiLanguage.TraditionalChinese => "隱藏表格",
        UiLanguage.SimplifiedChinese => "隐藏表格",
        _ => "Hide table"
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

    public string FormatUsageHistoryCount(int count, int visibleLimit) => Language switch
    {
        UiLanguage.TraditionalChinese =>
            count > visibleLimit ? $"本機 CSV 記錄 · 顯示最新 {visibleLimit} / {count} 筆" : $"本機 CSV 記錄 · {count} 筆",
        UiLanguage.SimplifiedChinese =>
            count > visibleLimit ? $"本地 CSV 记录 · 显示最新 {visibleLimit} / {count} 条" : $"本地 CSV 记录 · {count} 条",
        _ => count > visibleLimit
            ? $"Local CSV history · newest {visibleLimit} of {count} records"
            : $"Local CSV history · {count} records"
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

    public IReadOnlyList<ShortcutGroup> ShortcutGroups => new[]
    {
        new ShortcutGroup(
            MainWindowShortcutsGroup,
            new[]
            {
                new ShortcutHint("F1", ShowShortcutsShortcut),
                new ShortcutHint("S", OpenSettingsShortcut),
                new ShortcutHint("Ctrl+R", RefreshAction),
                new ShortcutHint("Esc", CloseWindowShortcut)
            }),
        new ShortcutGroup(
            SettingsWindowShortcutsGroup,
            new[]
            {
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
