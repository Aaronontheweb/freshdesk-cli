using FreshdeskCLI.Models;

namespace FreshdeskCLI.Helpers;

public static class EnumParser
{
    public static bool TryParseTicketStatus(string input, out TicketStatus status)
    {
        status = TicketStatus.None;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var normalizedInput = input.Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();

        var statusMappings = new Dictionary<string, TicketStatus>(StringComparer.OrdinalIgnoreCase)
        {
            { "open", TicketStatus.Open },
            { "pending", TicketStatus.Pending },
            { "resolved", TicketStatus.Resolved },
            { "closed", TicketStatus.Closed },
            { "waitingoncustomer", TicketStatus.WaitingOnCustomer },
            { "waitingcustomer", TicketStatus.WaitingOnCustomer },
            { "customerwait", TicketStatus.WaitingOnCustomer },
            { "waitingonthirdparty", TicketStatus.WaitingOnThirdParty },
            { "waitingthirdparty", TicketStatus.WaitingOnThirdParty },
            { "thirdpartywait", TicketStatus.WaitingOnThirdParty },
            { "2", TicketStatus.Open },
            { "3", TicketStatus.Pending },
            { "4", TicketStatus.Resolved },
            { "5", TicketStatus.Closed },
            { "6", TicketStatus.WaitingOnCustomer },
            { "7", TicketStatus.WaitingOnThirdParty }
        };

        if (statusMappings.TryGetValue(normalizedInput, out status))
            return true;

        if (Enum.TryParse<TicketStatus>(input, true, out status) && status != TicketStatus.None && Enum.IsDefined(typeof(TicketStatus), status))
            return true;

        return false;
    }

    public static string GetValidStatusValues()
    {
        return "Valid status values: Open, Pending, Resolved, Closed, WaitingOnCustomer, WaitingOnThirdParty";
    }

    public static bool TryParseTicketPriority(string input, out TicketPriority priority)
    {
        priority = TicketPriority.None;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var normalizedInput = input.ToLowerInvariant();

        var priorityMappings = new Dictionary<string, TicketPriority>(StringComparer.OrdinalIgnoreCase)
        {
            { "low", TicketPriority.Low },
            { "medium", TicketPriority.Medium },
            { "high", TicketPriority.High },
            { "urgent", TicketPriority.Urgent },
            { "1", TicketPriority.Low },
            { "2", TicketPriority.Medium },
            { "3", TicketPriority.High },
            { "4", TicketPriority.Urgent }
        };

        if (priorityMappings.TryGetValue(normalizedInput, out priority))
            return true;

        if (Enum.TryParse<TicketPriority>(input, true, out priority) && priority != TicketPriority.None && Enum.IsDefined(typeof(TicketPriority), priority))
            return true;

        return false;
    }

    public static string GetValidPriorityValues()
    {
        return "Valid priority values: Low, Medium, High, Urgent";
    }
}