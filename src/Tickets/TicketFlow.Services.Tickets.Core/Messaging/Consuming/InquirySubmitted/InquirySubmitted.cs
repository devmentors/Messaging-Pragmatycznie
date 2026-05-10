using TicketFlow.Shared.Messaging;

namespace TicketFlow.Services.Tickets.Core.Messaging.Consuming.InquirySubmitted;

public sealed record InquirySubmitted(Guid Id) : IMessage;