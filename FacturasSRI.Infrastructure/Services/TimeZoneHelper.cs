using System;
using FacturasSRI.Application.Interfaces;

namespace FacturasSRI.Infrastructure.Services
{
    public class TimeZoneHelper : ITimeZoneHelper
    {
        private readonly TimeZoneInfo _ecuadorTimeZone;

        public TimeZoneHelper()
        {
            try
            {
                _ecuadorTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    _ecuadorTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");
                }
                catch (TimeZoneNotFoundException)
                {
                    // Fallback to a fixed offset if the time zone is not found
                    _ecuadorTimeZone = TimeZoneInfo.CreateCustomTimeZone("Ecuador Standard Time", new TimeSpan(-5, 0, 0), "Ecuador Standard Time", "Ecuador Standard Time");
                }
            }
        }

        public DateTime ConvertUtcToEcuadorTime(DateTime utcDateTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _ecuadorTimeZone);
        }
    }
}
