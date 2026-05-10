using Microsoft.Extensions.Logging;
using TicketFlow.Services.Inquiries.Core.Data.Models;
using TicketFlow.Services.Inquiries.Core.Data.Repositories;
using TicketFlow.Services.Inquiries.Core.LanguageDetection;
using TicketFlow.Services.Inquiries.Core.Messaging.Publishing;
using TicketFlow.Shared.Commands;
using TicketFlow.Shared.Messaging;

namespace TicketFlow.Services.Inquiries.Core.Commands.SubmitInquiry;

internal sealed class SubmitInquiryHandler(IInquiriesRepository repository, ILanguageDetector languageDetector, 
    IMessagePublisher messagePublisher, ILogger<SubmitInquiryHandler> logger) : ICommandHandler<SubmitInquiry>
{
    private const string EnglishLanguageCode = "en";
    public async Task HandleAsync(SubmitInquiry command, CancellationToken cancellationToken = default)
    {
        var (name, email, title, description, category) = command;
        var categoryParsed = ParseCategory(category);
        var inquiry = new Inquiry(name, email, title, description, categoryParsed);
        
        await repository.AddAsync(inquiry, cancellationToken);
        var languageCode = await languageDetector.GetTextLanguageCode(inquiry.Description, cancellationToken);

        var inquiryReportedMessage = new InquirySubmitted(inquiry.Id);
        await messagePublisher.PublishAsync(inquiryReportedMessage, cancellationToken: cancellationToken);
        
        logger.LogInformation($"Inquiry with id: {inquiry.Id} submitted successfully.");
        
        if (languageCode is not EnglishLanguageCode)
        {
            var requestTranslationV1 = new RequestTranslationV1(inquiry.Description, inquiry.Id);
            
            await messagePublisher.PublishAsync(requestTranslationV1, destination: "", routingKey: "request-translation-v1-queue", cancellationToken: cancellationToken);
            
            logger.LogInformation($"Translation for inquiry with id: {inquiry.Id} has been requested.");
        }
    }

    private static InquiryCategory ParseCategory(string category)
    {
        CapitalizeInput();
        var parseSucceeded = Enum.TryParse<InquiryCategory>(category, out var categoryParsed);
        
        if (!parseSucceeded)
        {
            categoryParsed = InquiryCategory.Other;
        }

        return categoryParsed;

        void CapitalizeInput()
        {
            category = category[0].ToString().ToUpper() + category.Substring(1, category.Length - 1);
        }
    }
}