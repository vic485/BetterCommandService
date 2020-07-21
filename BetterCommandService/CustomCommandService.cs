﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BetterCommandService
{
    /// <summary>
    /// The Commmand class attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class DiscordCommandClass : Attribute
    {
        /// <summary>
        /// The prefix for all commands in this class.
        /// </summary>
        public string prefix { get; set; }
        /// <summary>
        /// If <see langword="true"/> then only the property prefix will work on child commands, if <see langword="false"/> then the assigned prefix AND the prefix on the command are valid. Default is <see langword="true"/>
        /// </summary>
        public bool OverwritesPrefix { get; set; }
        /// <summary>
        /// Tells the command service that this class contains commands.
        /// </summary>
        /// <param name="prefix">Prefix for the whole class, look at <see cref="OverwritesPrefix"/> for more options.</param>
        public DiscordCommandClass(string prefix)
        {
            this.prefix = prefix;
            OverwritesPrefix = true;
        }
        /// <summary>
        /// Tells the command service that this class contains commands.
        /// </summary>
        public DiscordCommandClass()
        {

        }
    }
    /// <summary>
    /// Discord command class
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DiscordCommand : Attribute
    {
        /// <summary>
        /// If <see langword="true"/> then <see cref="Settings.HasPermissionMethod"/> will be called and checked if the user has permission to execute the command, this result will be in the <see cref="CommandModuleBase"/>
        /// </summary>
        public bool RequiredPermission { get; set; }

        internal string commandName;
        /// <summary>
        /// The prefix for the command. This is optional, the command will work with the default prefix you passed or an overwrited one from the <see cref="DiscordCommandClass"/>
        /// </summary>
        public string[] prefixes { get; set; }
        /// <summary>
        /// Description of this command
        /// </summary>
        public string description { get; set; }
        /// <summary>
        /// Command help message, use this to create a generic help message
        /// </summary>
        public string commandHelp { get; set; }
        /// <summary>
        /// If <see langword="true"/> then bots can execute the command, default is <see langword="false"/>
        /// </summary>
        public bool BotCanExecute { get; set; }
        /// <summary>
        /// Tells the service that this method is a command
        /// </summary>
        /// <param name="commandName">the name of the command</param>
        public DiscordCommand(string commandName)
        {
            this.commandName = commandName;
            prefixes = new string[] { };
            BotCanExecute = false;
        }
        /// <summary>
        /// Tells the service that this method is a command
        /// </summary>
        /// <param name="commandName">the name of the command</param>
        /// <param name="Prefix">A prefix to overwrite the default one</param>
        public DiscordCommand(string commandName, string Prefix)
        {
            this.commandName = commandName;
            prefixes = new string[] { Prefix };
            BotCanExecute = false;
        }
        /// <summary>
        /// Tells the service that this method is a command
        /// </summary>
        /// <param name="commandName">the name of the command</param>
        /// <param name="Prefixes">The Prefix(es) of the command, if this is empty it will use the default prefix</param>
        public DiscordCommand(string commandName, params string[] Prefixes)
        {
            this.commandName = commandName;
            if (Prefixes.Length > 0)
                prefixes = Prefixes;
            else
                prefixes = new string[] { };
            BotCanExecute = false;
        }
    }
    /// <summary>
    /// The settings for the Command Service
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// The Default prefix for the command service
        /// </summary>
        public string DefaultPrefix { get; set; }
        /// <summary>
        /// This method will be called and when a command is called and checkes if the user has permission to execute the command, this result will be in the <see cref="CommandModuleBase.HasExecutePermission"/> if you dont set this, the <see cref="CommandModuleBase.HasExecutePermission"/> will always be <see langword="true"/>
        /// </summary>
        public Func<ICommandContext, bool> HasPermissionMethod { get; set; }
        /// <summary>
        /// A Dictionary containing specific permission methods for guilds, The Key would be the Guilds ID, and the value would be a Method that takes <see cref="SocketCommandContext"/> and returns a <see cref="bool"/>. this will overwrite the <see cref="HasPermissionMethod"/> if the guilds permission method is added
        /// </summary>
        public Dictionary<ulong, Func<ICommandContext, bool>> CustomGuildPermissionMethod { get; set; }
        /// <summary>
        /// Boolean indicating if commands can be accessable in Direct messages, default value is <see cref="false"/>
        /// </summary>
        [DefaultValue(false)]
        public bool DMCommands { get; set; }
        /// <summary>
        /// If the user has invalid permissions to execute the command and this bool is <see langword="true"/> then the command will still execute with the <see cref="CommandModuleBase.HasExecutePermission"/> set to <see langword="false"/>. Default is <see langword="false"/>
        /// </summary>
        public bool AllowCommandExecutionOnInvalidPermissions { get; set; }
    }
    /// <summary>
    /// The base class of <see cref="CustomCommandService"/>
    /// </summary>
    public class CustomCommandService
    {
        public List<string> UsedPrefixes { get; internal set; }

        public bool ContainsUsedPrefix(string msg)
        {
            return UsedPrefixes.Any(x => msg.StartsWith(x));
        }

        private List<Command> CommandList = new List<Command>();
        private class Command
        {
            public bool RequirePermission { get; set; }
            public string CommandName { get; set; }
            public string[] Prefixes { get; set; }
            public System.Reflection.ParameterInfo[] Paramaters { get; set; }
            public MethodInfo Method { get; set; }
            public DiscordCommand attribute { get; set; }
            public CommandClassobj parent { get; set; }
        }
        private class CommandClassobj
        {
            public string Prefix { get; set; }
            public DiscordCommandClass attribute { get; set; }
            public Type type { get; set; }
            public List<Command> ChildCommands { get; set; }
            public object ClassInstance { get; set; }

        }

        private static Settings currentSettings;
        private List<Type> CommandClasses { get; set; }

        /// <summary>
        /// Creates a new command service instance
        /// </summary>
        /// <param name="s">the <see cref="Settings"/> for the command service</param>
        public CustomCommandService(Settings s)
        {
            currentSettings = s;
            if (currentSettings.HasPermissionMethod == null)
                currentSettings.HasPermissionMethod = (ICommandContext s) => { return true; };
            if (s.CustomGuildPermissionMethod == null)
                currentSettings.CustomGuildPermissionMethod = new Dictionary<ulong, Func<ICommandContext, bool>>();
            CommandModuleBase.CommandDescriptions = new Dictionary<string, string>();
            CommandModuleBase.CommandHelps = new Dictionary<string, string>();
            CommandModuleBase.Commands = new List<ICommands>();
            Dictionary<MethodInfo, Type> CommandMethods = new Dictionary<MethodInfo, Type>();
            var Commandmethods = Assembly.GetEntryAssembly().GetTypes().SelectMany(x => x.GetMethods().Where(y => y.GetCustomAttributes(typeof(DiscordCommand), false).Length > 0).ToArray());
            foreach (var t in Commandmethods)
            {
                CommandMethods.Add(t, t.DeclaringType);
            }
            //add to base command list
            UsedPrefixes = new List<string>();
            UsedPrefixes.Add(currentSettings.DefaultPrefix);
            foreach (var item in CommandMethods)
            {
                var cmdat = item.Key.GetCustomAttribute<DiscordCommand>();
                var parat = item.Value.GetCustomAttribute<DiscordCommandClass>();
                if (cmdat.commandHelp != null)
                    if (!CommandModuleBase.CommandHelps.ContainsKey(cmdat.commandName))
                    {
                        CommandModuleBase.CommandHelps.Add(cmdat.commandName, cmdat.commandHelp);
                    }
                    else
                    {
                        CommandModuleBase.CommandHelps[cmdat.commandName] += "\n" + cmdat.commandHelp;
                    }
                if (cmdat.description != null)
                    if (!CommandModuleBase.CommandDescriptions.ContainsKey(cmdat.commandName))
                    {
                        CommandModuleBase.CommandDescriptions.Add(cmdat.commandName, cmdat.description);
                    }
                    else
                    {
                        CommandModuleBase.CommandDescriptions[cmdat.commandName] += "\n" + cmdat.description;
                    }

                Command cmdobj = new Command()
                {
                    CommandName = cmdat.commandName,
                    Method = item.Key,
                    Prefixes = cmdat.prefixes.Length == 0 ? new string[] { currentSettings.DefaultPrefix } : cmdat.prefixes,
                    attribute = cmdat,
                    parent = new CommandClassobj()
                    {
                        Prefix = parat == null ? currentSettings.DefaultPrefix : parat.prefix,
                        ClassInstance = Activator.CreateInstance(item.Value),
                        attribute = parat,
                    },
                    Paramaters = item.Key.GetParameters(),
                };
                CommandList.Add(cmdobj);

                var c = new Commands
                {
                    CommandName = cmdat.commandName,
                    CommandDescription = cmdat.description == null ? null : cmdat.description.Replace("(PREFIX)", string.Join("|", cmdobj.Prefixes)),
                    CommandHelpMessage = cmdat.commandHelp == null ? null : cmdat.commandHelp.Replace("(PREFIX)", string.Join("|", cmdobj.Prefixes)),
                    Prefixes = parat.prefix == "\0" ? cmdobj.Prefixes : cmdobj.Prefixes.Append(parat.prefix).ToArray(),
                    RequiresPermission = cmdat.RequiredPermission
                };
                CommandModuleBase.Commands.Add(c);

                foreach (var pr in cmdat.prefixes)
                    if (!UsedPrefixes.Contains(pr))
                        UsedPrefixes.Add(pr);
                if (!UsedPrefixes.Contains(parat.prefix) && parat.prefix != "\0")
                    UsedPrefixes.Add(parat.prefix);
            }
        }

        /// <summary>
        /// The command result object
        /// </summary>
        private class CommandResult : ICommandResult
        {
            public bool IsSuccess { get; set; }

            [DefaultValue(false)]
            public bool MultipleResults { get; set; }
            public CommandStatus Result { get; set; }
            public ICommandResult[] Results { get; set; }
            public Exception Exception { get; set; }
        }
        
        /// <summary>
        /// This method will execute a command if there is one
        /// </summary>
        /// <param name="context">The current discord <see cref="ICommandContext"/></param>
        /// <returns>The <see cref="ICommandResult"/> containing what the status of the execution is </returns>
        public async Task<ICommandResult> ExecuteAsync(ICommandContext context)
        {
            string[] param = context.Message.Content.Split(' ');
            param = param.TakeLast(param.Length - 1).ToArray();
            // TODO: This is still expecting char prefix
            string command = context.Message.Content.Split(' ').First().Remove(0, 1).ToLower();
            //string prefix = context.Message.Content.Split(' ').First().Toa().First();
            //check if the command exists
            if (!CommandList.Any(x => x.CommandName.ToLower() == command))
                return new CommandResult()
                {
                    Result = CommandStatus.NotFound,
                    IsSuccess = false
                };

            //Command exists

            //if more than one command with the same name exists then try to execute both or find one that matches the params
            var commandobj = CommandList.Where(x => x.CommandName.ToLower() == command);
            foreach (var item in commandobj)
                //if (!item.Prefixes.Contains(prefix)) //prefix doesnt match, check the parent class
                if (item.Prefixes.Any(command.StartsWith))
                {
                    var prefix = item.Prefixes.FirstOrDefault(command.StartsWith);
                    if (item.parent.attribute.prefix == "\0") //if theres no prefix
                        return new CommandResult() { Result = CommandStatus.NotFound, IsSuccess = false };
                    else //there is a prefix
                    {
                        if (item.parent.Prefix == prefix)
                            commandobj = commandobj.Where(x => x.parent.Prefix == prefix);
                        else
                            return new CommandResult() { Result = CommandStatus.NotFound, IsSuccess = false };
                    }

                }
                else //prefix match for method
                {
                    var prefix = item.Prefixes.FirstOrDefault(command.StartsWith);
                    if (item.parent.attribute.OverwritesPrefix)
                    {
                        if (item.parent.Prefix == prefix)
                        {
                            commandobj = commandobj.Where(x => x.parent.Prefix == prefix);
                        }
                        else
                            return new CommandResult() { Result = CommandStatus.NotFound, IsSuccess = false };
                    }
                    else
                    {
                        commandobj = commandobj.Where(x => x.Prefixes.Contains(prefix));
                    }
                }

            if (context.Channel.GetType() == typeof(SocketDMChannel) && !currentSettings.DMCommands)
                return new CommandResult() { IsSuccess = false, Result = CommandStatus.InvalidPermissions };

            if (commandobj.Any(x => x.Paramaters.Length == 0) && param.Length == 0)
            {
                try
                {
                    var cmd = commandobj.First(x => x.Paramaters.Length == 0);
                    cmd.parent.ClassInstance.GetType().GetProperty("Context").SetValue(cmd.parent.ClassInstance, context);

                    var d = (Func<Task>)Delegate.CreateDelegate(typeof(Func<Task>), cmd.parent.ClassInstance, cmd.Method);
                    await d();
                    //cmd.Method.Invoke(cmd.parent.ClassInstance, null);
                    return new CommandResult() { Result = CommandStatus.Success, IsSuccess = true };
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return new CommandResult() { Exception = ex, Result = CommandStatus.Error, IsSuccess = false };
                }
            }
            List<CommandResult> results = new List<CommandResult>();
            //1 find if param counts match
            foreach (var cmd in commandobj)
            {
                results.Add(await ExecuteCommand(cmd, context, param));
            }
            if (results.Any(x => x.IsSuccess))
                return results.First(x => x.IsSuccess);

            return new CommandResult() { Results = results.ToArray(), MultipleResults = true, IsSuccess = false };
        }
        private async Task<CommandResult> ExecuteCommand(Command cmd, ICommandContext context, string[] param)
        {
            if (!cmd.attribute.BotCanExecute && context.Message.Author.IsBot)
                return new CommandResult() { Result = CommandStatus.InvalidPermissions };
            if (cmd.Paramaters.Length == 0 && param.Length == 0)
            {
                try
                {
                    CommandModuleBase.HasExecutePermission
                    = currentSettings.CustomGuildPermissionMethod.ContainsKey(context.Guild.Id)
                    ? currentSettings.CustomGuildPermissionMethod[context.Guild.Id](context)
                    : currentSettings.HasPermissionMethod(context);

                    if (!currentSettings.AllowCommandExecutionOnInvalidPermissions && !CommandModuleBase.HasExecutePermission)
                        return new CommandResult() { IsSuccess = false, Result = CommandStatus.InvalidParams };

                    cmd.parent.ClassInstance.GetType().GetProperty("Context").SetValue(cmd.parent.ClassInstance, context);

                    var d = (Func<Task>)Delegate.CreateDelegate(typeof(Func<Task>), cmd.parent.ClassInstance, cmd.Method);
                    await d();
                    //cmd.Method.Invoke(cmd.parent.ClassInstance, null);
                    return new CommandResult() { Result = CommandStatus.Success, IsSuccess = true };
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return new CommandResult() { Exception = ex, Result = CommandStatus.Error, IsSuccess = false };
                }
            }
            else if (cmd.Paramaters.Length == 0 && param.Length > 0)
                return new CommandResult() { Result = CommandStatus.InvalidParams, IsSuccess = false };

            if (cmd.Paramaters.Last().GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
            {
                List<object> parsedparams = new List<object>();
                bool check = true;
                for (int i = 0; i != cmd.Paramaters.Length; i++)
                {
                    var dp = cmd.Paramaters[i];
                    if (dp.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
                    {
                        string[] arr = param.Skip(i).Where(x => x != "").ToArray();
                        if (arr.Length == 0)
                        {
                            ArrayList al = new ArrayList();
                            Type destyp = dp.ParameterType.GetElementType();
                            parsedparams.Add(al.ToArray(destyp));
                        }
                        else
                        {
                            ArrayList al = new ArrayList();
                            Type destyp = dp.ParameterType.GetElementType();
                            foreach (var item in arr)
                            {
                                if (TypeDescriptor.GetConverter(destyp).IsValid(item))
                                {
                                    //we can
                                    var pparam = TypeDescriptor.GetConverter(destyp).ConvertFromString(item);
                                    al.Add(pparam);
                                }
                                else
                                    check = false;
                            }
                            if (check)
                                parsedparams.Add(al.ToArray(destyp));
                        }
                    }
                    else
                    {
                        if (param.Length < cmd.Paramaters.Length - 1)
                            return new CommandResult() { Result = CommandStatus.InvalidParams, IsSuccess = false };
                        var p = param[i];
                        if (TypeDescriptor.GetConverter(dp.ParameterType).IsValid(p))
                        {
                            //we can
                            var pparam = TypeDescriptor.GetConverter(dp.ParameterType).ConvertFromString(p);
                            parsedparams.Add(pparam);
                        }
                        else
                            check = false;
                    }
                }
                //if it worked
                if (check)
                {
                    //invoke the method
                    try
                    {
                        CommandModuleBase.HasExecutePermission
                        = currentSettings.CustomGuildPermissionMethod.ContainsKey(context.Guild.Id)
                        ? currentSettings.CustomGuildPermissionMethod[context.Guild.Id](context)
                        : currentSettings.HasPermissionMethod(context);

                        if (!currentSettings.AllowCommandExecutionOnInvalidPermissions && !CommandModuleBase.HasExecutePermission)
                            return new CommandResult() { IsSuccess = false, Result = CommandStatus.InvalidParams };

                        cmd.parent.ClassInstance.GetType().GetProperty("Context").SetValue(cmd.parent.ClassInstance, context);

                        Task s = (Task)cmd.Method.Invoke(cmd.parent.ClassInstance, parsedparams.ToArray());
                        Task.Run(async () => await s).Wait();
                        if (s.Exception == null)
                            return new CommandResult() { Result = CommandStatus.Success, IsSuccess = true };
                        else
                            return new CommandResult() { Exception = s.Exception.InnerException, Result = CommandStatus.Error, IsSuccess = false };

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        return new CommandResult() { Exception = ex, Result = CommandStatus.Error, IsSuccess = false };
                    }

                }
                else
                    return new CommandResult() { Result = CommandStatus.InvalidParams, IsSuccess = false };
            }
            if (cmd.Paramaters.Length == param.Length)
            {
                List<object> parsedparams = new List<object>();
                bool check = true;

                //right command lengths met or params object in it now we gotta parse the params
                for (int i = 0; i != param.Length; i++)
                {
                    //PrimitiveParsers.Get(unparsedparam)
                    var p = param[i];
                    var dp = cmd.Paramaters[i];
                    //check if we can parse the value
                    if (TypeDescriptor.GetConverter(dp.ParameterType).IsValid(p))
                    {
                        //we can
                        var pparam = TypeDescriptor.GetConverter(dp.ParameterType).ConvertFromString(p);
                        parsedparams.Add(pparam);
                    }
                    else
                        check = false;
                }
                //if we parsed all the params correctly
                if (check)
                {
                    //invoke the method
                    try
                    {
                        CommandModuleBase.HasExecutePermission
                        = currentSettings.CustomGuildPermissionMethod.ContainsKey(context.Guild.Id)
                        ? currentSettings.CustomGuildPermissionMethod[context.Guild.Id](context)
                        : currentSettings.HasPermissionMethod(context);

                        if (!currentSettings.AllowCommandExecutionOnInvalidPermissions && !CommandModuleBase.HasExecutePermission)
                            return new CommandResult() { IsSuccess = false, Result = CommandStatus.InvalidParams };

                        cmd.parent.ClassInstance.GetType().GetProperty("Context").SetValue(cmd.parent.ClassInstance, context);

                        Task s = (Task)cmd.Method.Invoke(cmd.parent.ClassInstance, parsedparams.ToArray());
                        Task.Run(async () => await s).Wait();
                        if (s.Exception == null)
                            return new CommandResult() { Result = CommandStatus.Success, IsSuccess = true };
                        else
                            return new CommandResult() { Exception = s.Exception.InnerException, Result = CommandStatus.Error, IsSuccess = false };

                    }
                    catch (TargetInvocationException ex)
                    {
                        Console.WriteLine(ex);
                        return new CommandResult() { Exception = ex, Result = CommandStatus.Error, IsSuccess = false };
                    }

                }
                else
                    return new CommandResult() { Result = CommandStatus.InvalidParams, IsSuccess = false };

            }
            else
                return new CommandResult() { Result = CommandStatus.NotEnoughParams, IsSuccess = false };
        }
    }
}
