using Xunit;
using FreshdeskCLI.Models;
using FreshdeskCLI.Helpers;

namespace FreshdeskCLI.Tests.Helpers;

public class EnumParsingTests
{

    [Theory]
    [InlineData("Open", TicketStatus.Open)]
    [InlineData("open", TicketStatus.Open)]
    [InlineData("OPEN", TicketStatus.Open)]
    [InlineData("2", TicketStatus.Open)]
    [InlineData("Pending", TicketStatus.Pending)]
    [InlineData("3", TicketStatus.Pending)]
    [InlineData("Resolved", TicketStatus.Resolved)]
    [InlineData("4", TicketStatus.Resolved)]
    [InlineData("Closed", TicketStatus.Closed)]
    [InlineData("5", TicketStatus.Closed)]
    [InlineData("WaitingOnCustomer", TicketStatus.WaitingOnCustomer)]
    [InlineData("waiting on customer", TicketStatus.WaitingOnCustomer)]
    [InlineData("waiting-on-customer", TicketStatus.WaitingOnCustomer)]
    [InlineData("waiting_on_customer", TicketStatus.WaitingOnCustomer)]
    [InlineData("6", TicketStatus.WaitingOnCustomer)]
    [InlineData("WaitingOnThirdParty", TicketStatus.WaitingOnThirdParty)]
    [InlineData("waiting on third party", TicketStatus.WaitingOnThirdParty)]
    [InlineData("7", TicketStatus.WaitingOnThirdParty)]
    public void TryParseTicketStatus_ValidInputs_ReturnsCorrectStatus(string input, TicketStatus expected)
    {
        var result = EnumParser.TryParseTicketStatus(input, out var status);
        
        Assert.True(result);
        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData("InvalidStatus")]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("99")]
    [InlineData("OpenClosed")]
    public void TryParseTicketStatus_InvalidInputs_ReturnsFalse(string input)
    {
        var result = EnumParser.TryParseTicketStatus(input, out var status);
        
        Assert.False(result);
    }

    [Theory]
    [InlineData("Low", TicketPriority.Low)]
    [InlineData("low", TicketPriority.Low)]
    [InlineData("LOW", TicketPriority.Low)]
    [InlineData("1", TicketPriority.Low)]
    [InlineData("Medium", TicketPriority.Medium)]
    [InlineData("2", TicketPriority.Medium)]
    [InlineData("High", TicketPriority.High)]
    [InlineData("3", TicketPriority.High)]
    [InlineData("Urgent", TicketPriority.Urgent)]
    [InlineData("urgent", TicketPriority.Urgent)]
    [InlineData("4", TicketPriority.Urgent)]
    public void TryParseTicketPriority_ValidInputs_ReturnsCorrectPriority(string input, TicketPriority expected)
    {
        var result = EnumParser.TryParseTicketPriority(input, out var priority);
        
        Assert.True(result);
        Assert.Equal(expected, priority);
    }

    [Theory]
    [InlineData("InvalidPriority")]
    [InlineData("Critical")]
    [InlineData("")]
    [InlineData("99")]
    [InlineData("LowHigh")]
    public void TryParseTicketPriority_InvalidInputs_ReturnsFalse(string input)
    {
        var result = EnumParser.TryParseTicketPriority(input, out var priority);
        
        Assert.False(result);
    }
}