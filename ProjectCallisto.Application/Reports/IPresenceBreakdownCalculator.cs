using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Application.Reports;

public interface IPresenceBreakdownCalculator
{
    TimeBreakdown Calculate(
        List<PresenceHistory> records,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd);
}
