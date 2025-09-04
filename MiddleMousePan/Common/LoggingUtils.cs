using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem.LogThreads.Common;
using Kingmaker.UI.Models.Log.Enums;
using System.Linq;
using UnityEngine;

namespace MiddleMousePan.Common;
public static class LoggingUtils
{
    public static void CombatLog(string msg)
    {
        CombatLogMessage message;
        message = new CombatLogMessage(msg, Color.black, PrefixIcon.None, null, false);
        var messageLog = LogThreadService.Instance.m_Logs[LogChannelType.Common].First(x => x is WarningNotificationLogThread);
        messageLog.AddMessage(message);
    }
}
