using System.Text.Json;
using TicketFlow.CourseUtils;
using TicketFlow.Services.Tickets.Core.Data.Models;
using TicketFlow.Services.Tickets.Core.Data.Repositories;
using TicketFlow.Shared.Exceptions;
using TicketFlow.Shared.Messaging;
using TicketFlow.Shared.Serialization;

namespace TicketFlow.Services.Tickets.Core.Messaging.Consuming.InquirySubmitted;

public sealed class InquirySubmittedHandler(ITicketsRepository repository, IMessagePublisher messagePublisher) : IMessageHandler<InquirySubmitted>
{
    private static HttpClient _httpClient = new();
    
    public async Task HandleAsync(InquirySubmitted message, CancellationToken cancellationToken = default)
    {
        if (!FeatureFlags.UseListenToYourselfExample)
        {
            await HandleDefault(message, cancellationToken);
        }
        else
        {
            await HandleWithListenToYourself(message, cancellationToken);
        }
    }

    private async Task HandleDefault(InquirySubmitted message, CancellationToken cancellationToken)
    {
        var inquiry = await GetInquiryDetailsAsync(message.Id, cancellationToken);

        if (inquiry == null)
        {
            throw new TicketFlowException("Inquiry not found");
        }
        
        var (id, name, title, email, description, category, _, _, _) = inquiry;

        if (await repository.ExistsAsync(id, cancellationToken))
        {
            return;
        }

        var categoryValid = Enum.TryParse<TicketCategory>(category, out var categoryParsed);
        if (categoryValid is false)
        {
            categoryParsed = TicketCategory.Other;
        }
        
        var ticket = new Ticket(id, name, email, title, description, categoryParsed, "unknown");
        
        var scheduledAction = await repository.GetScheduledAction(message.Id, cancellationToken);

        if (scheduledAction is not null)
        {
            ticket.SetTranslation(scheduledAction.TranslatedText);
        }
        else if (ticket.IsEnglish is false)
        {
            ticket.WaitForScheduledActions();
        }
        
        await repository.AddAsync(ticket, cancellationToken);

        var ticketCreatedMessage = new Publishing.TicketCreated(
            Id: ticket.Id, 
            InquiryId: message.Id, 
            Name: ticket.Name,
            Email: ticket.Email,
            Title: ticket.Title,
            Description: ticket.Description,
            Category: ticket.Category.ToString(),
            LanguageCode: ticket.LanguageCode);
        
        await messagePublisher.PublishAsync(ticketCreatedMessage, cancellationToken: cancellationToken);
    }
    
    private async Task HandleWithListenToYourself(InquirySubmitted message, CancellationToken cancellationToken)
    {
        var inquiry = await GetInquiryDetailsAsync(message.Id, cancellationToken);

        if (inquiry == null)
        {
            throw new TicketFlowException("Inquiry not found");
        }
        
        var (id, name, title, email, description, category, _, _, _) = inquiry;

        if (await repository.ExistsAsync(id, cancellationToken))
        {
            return;
        }

        var categoryValid = Enum.TryParse<TicketCategory>(category, out var categoryParsed);
        if (categoryValid is false)
        {
            categoryParsed = TicketCategory.Other;
        }
        
        var ticket = new Ticket(Guid.NewGuid(), name, email, title, description, categoryParsed, "unknown");
        
        var scheduledAction = await repository.GetScheduledAction(message.Id, cancellationToken);

        if (scheduledAction is not null)
        {
            ticket.SetTranslation(scheduledAction.TranslatedText);
        }
        else if (ticket.IsEnglish is false)
        {
            ticket.WaitForScheduledActions();
        }

        var ticketCreatedMessage = new Publishing.TicketCreated(
            Id: ticket.Id, 
            InquiryId: message.Id, 
            Name: ticket.Name,
            Email: ticket.Email,
            Title: ticket.Title,
            Description: ticket.Description,
            Category: ticket.Category.ToString(),
            LanguageCode: ticket.LanguageCode);
        
        await messagePublisher.PublishAsync(ticketCreatedMessage, cancellationToken: cancellationToken);
    }

    private async Task<InquiryDto> GetInquiryDetailsAsync(Guid inquiryId, CancellationToken cancellationToken)
    {
        try
        {
            var json = await _httpClient.GetStringAsync($"http://localhost:5011/inquiries/{inquiryId.ToString()}", cancellationToken);
            var result = JsonSerializer.Deserialize<InquiryDto>(json, SerializationOptions.Default);
            return result!;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(ex.Message);
            return null!;
        }
    }
    
    public record InquiryDto(
        Guid Id,
        string Name,
        string Title,
        string Email,
        string Description,
        string Category,
        string Status,
        string CreatedAt,
        string? TicketId);
}