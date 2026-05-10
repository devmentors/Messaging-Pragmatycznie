using TicketFlow.Shared.Messaging;

namespace TicketFlow.Services.Inquiries.Core.Messaging.Publishing;

public sealed record InquirySubmitted(Guid Id) : IMessage;