using System;

namespace FacturasSRI.Application.Interfaces
{
    public interface ITimeZoneHelper
    {
        DateTime ConvertUtcToEcuadorTime(DateTime utcDateTime);
    }
}
