using Google.Api.Gax;
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
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{
    public delegate string ProcessDelegate();

    class Program
    {
        private static TelegramBotClient bot;
        private static Dictionary<string, ProcessDelegate> intentHandlers;
        private static IConversationRepo conversations;
        private static CancellationTokenSource cts = new CancellationTokenSource();
        private static IConfiguration configuration;
        private static string agentId;
        private static string dialogFlowAPIKeyFile;
        private static string botToken;

        public static async Task Main()
        {
            configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
            agentId = configuration.GetValue<string>("AgentId");
            dialogFlowAPIKeyFile = configuration.GetValue<string>("DialogFlowAPIFile");
            botToken = configuration.GetValue<string>("BotToken");

            conversations = new RamConversationRepo();

            await IntentDetector.CreateAsync(agentId, dialogFlowAPIKeyFile);


            bot = new TelegramBotClient(botToken);
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

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            Conversation conversation;
            if (update.Type == UpdateType.Message)
            {
                if (update.Message?.EntityValues != null) //message is a command
                {
                    if (update.Message.EntityValues.FirstOrDefault().Equals("/start"))
                    {
                        await HandleNewUserAsync(update);
                    }
                }

                conversation = await CheckForExistingConversation(update.Message.Chat.Id);

                await conversation.HandleUserInputAsync(update);
            }
            else if(update.Type == UpdateType.CallbackQuery)
            {
                conversation = await CheckForExistingConversation(update.CallbackQuery.From.Id);

                await conversation.HandleUserInputAsync(update);
            }

            //if (update.Type == UpdateType.Message)
            //{
            //    if (update.Message.Document != null)
            //    {
            //        using FileStream fileStream = new FileStream(update.Message.Document.FileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);

            //        var tmp = await bot.GetInfoAndDownloadFileAsync(update.Message.Document.FileId, fileStream);

            //        if (Path.GetExtension(update.Message.Document.FileName) == ".stl")
            //        {

            //            await HandleStlInputAsync(update, user);
            //        }
            //        else if (Path.GetExtension(update.Message.Document.FileName) == ".gcode")
            //        {
            //            await SendMessageAsync(update.Message.Chat.Id, "got gcode");
            //        }
            //        else
            //        {
            //            await SendMessageAsync(update.Message.Chat.Id, "other file");
            //        }
            //    }
            //    else if (update.Message.EntityValues != null) //message is a command
            //    {
            //        if (update.Message.EntityValues.FirstOrDefault().Equals("/start"))
            //        {
            //            await HandleNewUserAsync(update);
            //        }
            //    }
            //    else
            //    {


            //        if (user != null)
            //        {
            //            switch (user.CurrentState)
            //            {
            //                case ConversationState.WaitingForUserName:
            //                    await SendNameResponseMessage(update, user);
            //                    break;
            //                case ConversationState.Unknown:
            //                    break;
            //                case ConversationState.WorkflowTutorial:
            //                case ConversationState.HardwareTutorial:
            //                    await HandleUserInputDuringTutorialAsync(update, user);
            //                    break;
            //                case ConversationState.Idle:
            //                    await HandleUserInputAsync(update, user, cancellationToken);
            //                    break;
            //                case ConversationState.WaitingForConfimationToStartWorkflowTutorial | ConversationState.Idle:
            //                    await HandleUserInputAfterHardwareTutorialAsync(update, user);
            //                    break;
            //                case ConversationState.ReceivedStlFile:

            //                    break;
            //                default:
            //                    break;
            //            }
            //        }
            //        else
            //        {
            //            await HandleNewUserAsync(update);
            //        }
            //    }
            //}
        }

        /// <summary>
        /// checks for existing conversation. creates a new one if not found
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private async static Task<Conversation> CheckForExistingConversation(long id)
        {
            var conversation = conversations.GetConversationById(id);
            if (conversation == null)
            {
                conversation = new Conversation(id, bot);
                await conversation.StartAsync();
                conversations.AddConversation(id, conversation);
            }
            return conversation;
        }

        //private static async Task HandleStlInputAsync(Update update, Conversation user)
        //{
        //    user.CurrentState = ConversationState.ReceivedStlFile;
        //    var keyboard =  new ReplyKeyboardMarkup(
        //                    new KeyboardButton[] { "Nein", "Ja" },
        //                    resizeKeyboard: true
        //                );
        //    await SendMessageAsync(update.Message.Chat.Id, "I got your model. Should I slice it for you?", keyboard);



        //}

        //private static async Task HandleUserInputAfterHardwareTutorialAsync(Update update, Conversation user)
        //{
        //    var response = await CallDFAPIAsync(update.Message, "TutorialStarten-followup");

        //    switch (response)
        //    {
        //        case TutorialYes intent:
        //            await StartWorkflowTutorialAsync(user);
        //            break;
        //        case TutorialNo intent:
        //            user.CurrentState = ConversationState.Idle;
        //            await SendMessageAsync(user.Id, "Ok. Was kann ich sonst für dich tun?", new ReplyKeyboardRemove());
        //            break;
        //        default:
        //            break;
        //    }
        //}

        //private static async Task StartWorkflowTutorialAsync(Conversation user)
        //{
        //    user.CurrentState = ConversationState.WorkflowTutorial;

        //    ITutorialDataProvider data = new WorkflowTutorialDataProvider();
        //    WorkflowTutorial tutorial = new WorkflowTutorial(user.Id, bot, data);
        //    user.tutorial = tutorial;
        //    await tutorial.NextAsync();

        //}

        //private static async Task HandleUserInputDuringTutorialAsync(Update update, Conversation user)
        //{
        //    var intent = await CallDFAPIAsync(update.Message, "TutorialStarten-followup");

        //    if (intent is TutorialNext)
        //    {
        //        if(await user.Tutorial.NextAsync())
        //        {
        //            if (user.CurrentState == ConversationState.HardwareTutorial)
        //            {
        //                user.CurrentState = ConversationState.Idle | ConversationState.WaitingForConfimationToStartWorkflowTutorial;
        //            }
        //            else if(user.CurrentState == ConversationState.WorkflowTutorial)
        //            {
        //                user.CurrentState = ConversationState.Idle;
        //            }
        //            else
        //            {
        //                //we should not come here.
        //                throw new Exception("Invalid user state.");
        //            }
        //        }
        //    }
        //    else if (intent is TutorialCancel)
        //    {
        //        await user.Tutorial.CancelAsync();
        //        user.CurrentState = ConversationState.Idle;
        //    }
        //    else
        //    {
        //        await SendMessageAsync(user.Id, "Ich habe dich nicht verstanden");
        //    }
        //}

        private static async Task HandleNewUserAsync(Update update)
        {
            var conversation = new Conversation(update.Message.Chat.Id, bot);
            conversations.AddConversation(update.Message.Chat.Id, conversation);
            await conversation.StartAsync();
        }

        //private static async Task HandleUserInputAsync(Update update, Conversation user, CancellationToken cancellationToken)
        //{
        //    try
        //    {
        //        await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);

        //        var response = await CallDFAPIAsync(update.Message);

        //        switch (response)
        //        {
        //            case TutorialStartIntent intent:
        //                {
        //                    await StartHardwareTutorialAsync(user);
        //                    break;
        //                }
        //            case DefaultFallbackIntent intent:
        //                {
        //                    await SendMessageAsync(user.Id, intent.Process());
        //                    break;
        //                }
        //            case WelcomeIntent intent:
        //                {
        //                    await SendMessageAsync(user.Id, intent.Process());
        //                    break;
        //                }
        //            default:
        //                await SendMessageAsync(user.Id, "Intent detected:" + response.GetType() +". There is no implementation for this intent yet.");
        //                break;
        //        }
        //    }
        //    catch (Exception exception)
        //    {
        //        await HandleErrorAsync(bot, exception, cancellationToken);
        //    }
        //}


        //private static async Task StartHardwareTutorialAsync(Conversation user)
        //{
        //    user.CurrentState = ConversationState.HardwareTutorial;

        //    ITutorialDataProvider data = new HardwareTutorialDataProvider();
        //    HardwareTutorial tutorial = new HardwareTutorial(user.Id, bot, data);
        //    user.tutorial = tutorial;
        //    await tutorial.NextAsync();
        //}



        //private static async Task SendMessageAsync(Int64 chatId, string text, IReplyMarkup replyKeyboardMarkup = null)
        //{
        //    await bot.SendTextMessageAsync(chatId: chatId,
        //                    text: text,
        //                    replyMarkup: replyKeyboardMarkup);
        //}

        //private static async Task SendNameResponseMessage(Update update, Conversation user)
        //{
        //    var text = "Hi " + update.Message.Text + ". Was kann ich für dich tun?";
        //    user.CurrentState = ConversationState.Idle;
        //    user.UserName = update.Message.Text;
        //    await bot.SendTextMessageAsync(chatId: update.Message.Chat.Id,
        //                    text: text,
        //                    replyMarkup: new ReplyKeyboardRemove());
        //}

        //private static async Task SendWelcomeMessageAsync(Update update)
        //{
        //    await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);

        //    var text = "Welcome Message: My name is Bot. I can do stuff. How should i call you?";

        //    await bot.SendTextMessageAsync(chatId: update.Message.Chat.Id,
        //                                    text: text,
        //                                    replyMarkup: new ReplyKeyboardRemove());
        //}

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

        //public static async Task<object> CallDFAPIAsync(Telegram.Bot.Types.Message message, string contextName = null, bool clearContext = true)
        //{
        //    var sessionId = message.Chat.Id;

        //    var query = new QueryInput
        //    {
        //        Text = new TextInput
        //        {
        //            Text = message.Text,
        //            LanguageCode = "de"
        //        }
        //    };

        //    var request = new DetectIntentRequest
        //    {
        //        SessionAsSessionName = new SessionName(agentId, sessionId.ToString()),
        //        QueryInput = query,
        //        QueryParams = new QueryParameters()
        //    };

        //    if (contextName != null)
        //    {
        //        var context = new Context
        //        {
        //            ContextName = new ContextName(agentId, sessionId.ToString(), contextName),
        //            LifespanCount = 3,
        //        };
        //        request.QueryParams.Contexts.Add(context);
        //    }
        //    else
        //    {
        //        request.QueryParams.ResetContexts = clearContext;
        //    }

        //    SessionsClient client = new SessionsClientBuilder
        //    {
        //        CredentialsPath = dialogFlowAPIKeyFile
        //    }.Build();

        //    var response = await client.DetectIntentAsync(request);

        //    var type = intentMap[response.QueryResult.Intent.DisplayName]; //key not found exception could be thrown here
        //    var intent = (BaseIntent)Activator.CreateInstance(type);
        //    intent.response = response;
        //    return intent;
        //}

        //private static string ProcessIntent(DetectIntentResponse response)
        //{
        //    ProcessDelegate processDelegate;
        //    string returnString;
        //    if (intentHandlers.TryGetValue(response.QueryResult.Intent.DisplayName, out processDelegate))
        //    {
        //        if (processDelegate != null)
        //        {
        //            try
        //            {
        //                returnString = processDelegate(response);
        //            }
        //            catch (NotImplementedException ex)
        //            {
        //                returnString = ex.Message;
        //            }
        //            catch (Exception)
        //            {
        //                returnString = "Es ist ein Fehler aufgetreten";
        //            }
        //        }
        //        else
        //        {
        //            returnString = "Keine passende Process-Methode gefunden";
        //        }
        //    }
        //    else
        //    {
        //        returnString = "Intent nicht in der Middleware gefunden";
        //    }
        //    return returnString;
        //}
    }
}
