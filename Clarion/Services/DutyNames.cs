using System;
using Lumina.Excel.Sheets;

namespace Clarion.Services;

/// <summary>Resolves a recruitment DutyId (a ContentFinderCondition row id) to its display name.</summary>
public static class DutyNames
{
    public static string Get(uint dutyId)
    {
        if (dutyId == 0)
            return string.Empty;

        try
        {
            var sheet = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>();
            if (sheet != null && sheet.TryGetRow(dutyId, out var row))
            {
                var name = row.Name.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "DutyNames.Get failed for {0}", dutyId);
        }

        return string.Empty;
    }
}
