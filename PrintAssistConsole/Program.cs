﻿using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using Google.Cloud.Storage.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
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
    public delegate string ProcessDelegate(DetectIntentResponse response);

    class Program
    {
        private static TelegramBotClient bot;
        private static Dictionary<string, ProcessDelegate> intentHandlers;
        private static IUserRepo users;
        private static CancellationTokenSource cts = new CancellationTokenSource();
        private static IConfiguration configuration;
        private static string agentId;
        private static string dialogFlowAPIKeyFile;

        public static async Task Main()
        {
            configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
            agentId = configuration.GetValue<string>("AgentId");
            dialogFlowAPIKeyFile = configuration.GetValue<string>("DialogFlowAPIFile");

            users = new RamUserRepo();

            SetupIntentMapping(agentId);

            bot = new TelegramBotClient(TelegramBotConfiguration.BotToken);
            var me = await bot.GetMeAsync();
            bot.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync), cts.Token);


            Console.WriteLine($"Start listening for @{me.Username}");
            Console.WriteLine("Press CTRL+C to exit.");
            
            Console.CancelKeyPress += Console_CancelKeyPress;
            while (true)
            {
                Thread.Sleep(100);
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            // Send cancellation request to stop bot
            cts.Cancel();
            System.Environment.Exit(0);
        }

        private static void SetupIntentMapping(string agentId)
        {
            intentHandlers = new Dictionary<string, ProcessDelegate>();

            //Get all types with custom attribute IntentAttribute in assembly
            var intentClasses =
            from a in AppDomain.CurrentDomain.GetAssemblies()
            from t in a.GetTypes()
            let attributes = t.GetCustomAttributes(typeof(IntentAttribute), true)
            where attributes != null && attributes.Length > 0
            select new { Type = t, Attributes = attributes.Cast<IntentAttribute>() };

            foreach (var type in intentClasses)
            {
                //get intent name from attribute
                var intentName = ((IntentAttribute)(Attribute.GetCustomAttribute(type.Type, typeof(IntentAttribute)))).IntentName;
                
                //get method info for Process method
                var mi = type.Type.GetMethod("Process", BindingFlags.Public | BindingFlags.Static);
                
                if (mi != null)
                {
                    //store intent name and connected Process method in dictionary
                    intentHandlers.Add(intentName, (ProcessDelegate)mi.CreateDelegate(typeof(ProcessDelegate), null));
                }
                else
                {
                    throw new NotImplementedException("Process method is not implemented for class: " + type.Type.Name);
                }
            }

            var intents = GetAllIntentsForAgent(agentId);
            foreach (var intent in intents)
            {
                //check if all intents are implemented
                if(!intentHandlers.ContainsKey(intent.DisplayName))
                {
                    Console.WriteLine("No class for " + intent.DisplayName);
                }
            }
        }

        private static Page<Intent> GetAllIntentsForAgent(string agentId)
        {
            ListIntentsRequest request = new ListIntentsRequest
            {
                Parent = "projects/"+ agentId + "/agent"
            };

            IntentsClient client = new IntentsClientBuilder
            {
                CredentialsPath = @"C:\Users\Florian\DF-APIKEY-printassist.json"
            }.Build();

            var intents = client.ListIntents(request);

            //ToDo: check if there are more than 100 intents
            return intents.ReadPage(100);
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message)
            {
                if (update.Message.EntityValues != null) //is command
                {
                    if (update.Message.EntityValues.FirstOrDefault().Equals("/start"))
                    {
                        await SendWelcomeMessageAsync(update);
                        users.AddUser(update.Message.Chat.Id, new Classes.User(update.Message.Chat.Id) { CurrentState = UserState.WaitingForUserName});
                    }
                }
                else
                {
                    var user = users.GetUserById(update.Message.Chat.Id);
                    
                    if(user != null)
                    {
                        if(user.CurrentState == UserState.WaitingForUserName)
                        {
                            await SendNameResponseMessage(update, user);
                        }
                        else
                        {
                            try
                            {
                                await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);

                                var response = await CallDFAPIAsync(update.Message);

                                var text = ProcessIntent(response);
                                
                                await bot.SendTextMessageAsync(chatId: update.Message.Chat.Id,
                                                                text: text,
                                                                replyMarkup: new ReplyKeyboardRemove());
                            }
                            catch (Exception exception)
                            {
                                await HandleErrorAsync(botClient, exception, cancellationToken);
                            }
                        }
                    }
                }
            }
        }

        private static async Task SendNameResponseMessage(Update update, Classes.User user)
        {
            var text = "Hi " + update.Message.Text + ". Was kann ich für dich tun?";
            user.CurrentState = UserState.Idle;
            user.Name = update.Message.Text;
            await bot.SendTextMessageAsync(chatId: update.Message.Chat.Id,
                            text: text,
                            replyMarkup: new ReplyKeyboardRemove());
        }

        private static async Task SendWelcomeMessageAsync(Update update)
        {
            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);

            var text = "Welcome Message: My name is Bot. I can do stuff. How should i call you?";

            await bot.SendTextMessageAsync(chatId: update.Message.Chat.Id,
                                            text: text,
                                            replyMarkup: new ReplyKeyboardRemove());
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

        public static async Task<DetectIntentResponse> CallDFAPIAsync(Telegram.Bot.Types.Message message)
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
                CredentialsPath = dialogFlowAPIKeyFile
            }.Build();

            var sessionId = message.Chat.Id;
            var response = await client.DetectIntentAsync(
                new SessionName(agentId, sessionId.ToString()),
                query
            );
            return response;
        }

        private static string ProcessIntent(DetectIntentResponse response)
        {
            ProcessDelegate processDelegate;
            string returnString;
            if (intentHandlers.TryGetValue(response.QueryResult.Intent.DisplayName, out processDelegate))
            {
                if (processDelegate != null)
                {
                    try
                    {
                        returnString = processDelegate(response);
                    }
                    catch (NotImplementedException ex)
                    {
                        returnString = ex.Message;
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
