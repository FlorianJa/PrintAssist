﻿using Google.Api.Gax.Grpc;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using Google.Cloud.Storage.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Auth;
using PrintAssistConsole.Intents;
using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{
    class Program
    {
        private static TelegramBotClient Bot;
        public static Dictionary<string, MethodInfo> IntentHandlers { get; private set; }
        public static async Task Main()
        {

            var typesWithMyAttribute =
            from a in AppDomain.CurrentDomain.GetAssemblies()
            from t in a.GetTypes()
            let attributes = t.GetCustomAttributes(typeof(IntentAttribute), true)
            where attributes != null && attributes.Length > 0
            select new { Type = t, Attributes = attributes.Cast<IntentAttribute>() };


            IntentHandlers = new Dictionary<string, MethodInfo>();
            foreach (var type in typesWithMyAttribute)
            {
                var intentName = ((IntentAttribute)(Attribute.GetCustomAttribute(type.Type, typeof(IntentAttribute)))).IntentName;
                IntentHandlers.Add(intentName, type.Type.GetMethod("Process"));
            }

            Bot = new TelegramBotClient(TelegramBotConfiguration.BotToken);

            var me = await Bot.GetMeAsync();
            Console.Title = me.Username;

            var cts = new CancellationTokenSource();

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            Bot.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync),
                               cts.Token);

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            // Send cancellation request to stop bot
            cts.Cancel();
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {

            //var handler = update.Type switch
            //{
            //    // UpdateType.Unknown:
            //    // UpdateType.ChannelPost:
            //    // UpdateType.EditedChannelPost:
            //    // UpdateType.ShippingQuery:
            //    // UpdateType.PreCheckoutQuery:
            //    // UpdateType.Poll:
            //    UpdateType.Message => CallDFAPIAsync(update.Message.Text),
            //    //UpdateType.EditedMessage => BotOnMessageReceived(update.Message),
            //    //UpdateType.CallbackQuery => BotOnCallbackQueryReceived(update.CallbackQuery),
            //    //UpdateType.InlineQuery => BotOnInlineQueryReceived(update.InlineQuery),
            //    //UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(update.ChosenInlineResult),
            //    //_ => UnknownUpdateHandlerAsync(update)
            //};
            if (update.Type == UpdateType.Message)
            {
                try
                {
                    await Bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                    var text = await CallDFAPIAsync(update.Message);
                    await Bot.SendTextMessageAsync(chatId: update.Message.Chat.Id,
                                                    text: text,
                                                    replyMarkup: new ReplyKeyboardRemove());
                    //await handler;
                }
                catch (Exception exception)
                {
                    await HandleErrorAsync(botClient, exception, cancellationToken);
                }
            }
        }

        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public static async Task<String> CallDFAPIAsync(Telegram.Bot.Types.Message message)
        {
            var query = new QueryInput
            {
                Text = new TextInput
                {
                    Text = message.Text,
                    LanguageCode = "de"
                }
            };

            SessionsClient client = new SessionsClientBuilder
            {
                CredentialsPath = @"C:\Users\Florian\DF-APIKEY-printassist.json"
            }.Build();

            var sessionId = message.Chat.Id;
            var agent = "printassist-jxgl";

            var reponse = await client.DetectIntentAsync(
                new SessionName(agent, sessionId.ToString()),
                query
            );


            MethodInfo methodInfo;
            string returnString;
            if (IntentHandlers.TryGetValue(reponse.QueryResult.Intent.DisplayName, out methodInfo))
            {
                if (methodInfo != null)
                {
                    try
                    {
                        returnString = (string)methodInfo.Invoke(null, new object[] { reponse });
                    }
                    catch (Exception)
                    {
                        returnString = "Es ist ein Fehler aufgetreten";
                    }
                }
                else
                {
                    returnString = "Keine passende Process-Methode gefunden";
                }
            }
            else
            {
                returnString = "Intent nicht in der Middleware gefunden";
            }
            return returnString;
        }
    }
}
