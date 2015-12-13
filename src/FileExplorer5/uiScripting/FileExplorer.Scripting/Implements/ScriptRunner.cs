﻿using Common.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileExplorer.Utils;

namespace FileExplorer.Scripting
{   
    public class ScriptRunner : IScriptRunner
    {
        private static ILog logger = LogManager.GetLogger<ScriptRunner>();
        public static ScriptRunner Instance = new ScriptRunner();

        #region RunScript/Async(initialParameters, cloneParameters, commands[])
        public static Task RunScriptAsync(IParameterDic initialParameters, bool cloneParameters, params IScriptCommand[] commands)
        {
            if (cloneParameters)
                initialParameters = initialParameters.Clone();

            IScriptRunner runner = initialParameters.Get<IScriptRunner>("{ScriptRunner}", Instance);
            return runner.RunAsync(new Queue<IScriptCommand>(commands), initialParameters);
        }

        public static void RunScript(IParameterDic initialParameters, bool cloneParameters, params IScriptCommand[] commands)
        {
            if (cloneParameters)
                initialParameters = initialParameters.Clone();

            IScriptRunner runner = initialParameters.Get<IScriptRunner>("{ScriptRunner}", Instance);
            runner.Run(new Queue<IScriptCommand>(commands), initialParameters);
        }
        #endregion

        #region RunScript/Async(initialParameters, commands[])
        public static Task RunScriptAsync(IParameterDic initialParameters, params IScriptCommand[] commands)
        {
            return RunScriptAsync(initialParameters, false, commands);
        }

        public static void RunScript(IParameterDic initialParameters, params IScriptCommand[] commands)
        {
            RunScript(initialParameters, false, commands);
        }
        #endregion

        #region RunScript/Async<T>(initialParameters, resultVariable, commands[])

        public static async Task<T> RunScriptAsync<T>(string resultVariable = "{Result}", IParameterDic initialParameters = null, 
            params IScriptCommand[] commands)
        {
            initialParameters = initialParameters ?? new ParameterDic();
            await RunScriptAsync(initialParameters, false, commands);
            return initialParameters.Get(resultVariable, default(T));
        }

        public static T RunScript<T>(string resultVariable = "{Result}", IParameterDic initialParameters = null,
            params IScriptCommand[] commands)
        {
            initialParameters = initialParameters ?? new ParameterDic();
            RunScript(initialParameters, false, commands);
            return initialParameters.Get(resultVariable, default(T));
        }

        #endregion

        #region #region RunScript/Async(commands[])
        public static Task RunScriptAsync(params IScriptCommand[] commands)
        {
            return RunScriptAsync(new ParameterDic(), commands);
        }

        public static void RunScript(params IScriptCommand[] commands)
        {
            RunScript(new ParameterDic(), commands);
        }
        #endregion


        public ScriptRunner()
        {

        }


        public void Run(Queue<IScriptCommand> cmds, IParameterDic initialParameters)
        {
            IParameterDic pd = initialParameters;
            pd.ScriptRunner(this);

            while (cmds.Any())
            {
                try
                {
                    var current = cmds.Dequeue();
                    //logger.Info("Running " + current.CommandKey);
                    if (current.CanExecute(pd))
                    {
                        //(pd.Progress<IScriptCommand>() as IProgress<IScriptCommand>).Report()
                        //pd.CommandHistory.Add(current.CommandKey);
                        var retCmd = current.Execute(pd);
                        if (retCmd != null)
                        {
                            if (pd.Error() != null)
                                throw pd.Error();
                            cmds.Enqueue(retCmd);
                        }

                    }
                    else
                        if (!(current is NullScriptCommand))                        
                            throw new Exception(String.Format("Cannot execute {0}", current));
                }
                catch (Exception ex)
                {
                    pd.Error(ex);
                    logger.Error("Error when running script", ex);
                    
                    var progress = pd.Progress<Defines.TransferProgress>();
                    if (progress != null)
                        progress.Report(Defines.TransferProgress.Error(ex));
                    throw ex;
                }
            }
        }

        public void Run(IScriptCommand cmd, ParameterDic initialParameters)
        {
            Run(new Queue<IScriptCommand>(new[] { cmd }), initialParameters);
        }

        public async Task RunAsync(Queue<IScriptCommand> cmds, IParameterDic initialParameters)
        {
            IParameterDic pd = initialParameters;
            pd.ScriptRunner(this);

            while (cmds.Any())
            {

                try
                {
                    var current = cmds.Dequeue();

                    if (current.CanExecute(pd))
                    {
                        //pd.CommandHistory.Add(current.CommandKey);
                        //logger.Info("Running " + current.CommandKey);

                        try
                        {
                            var retCmd = await current.ExecuteAsync(pd)
                                .ConfigureAwait(current.RequireCaptureContext());

                            if (retCmd != null)
                            {
                                if (pd.Error() != null)
                                {
                                    logger.Error("Error when running script", pd.Error());
                                    return;
                                }
                                cmds.Enqueue(retCmd);
                            }
                        }
                        catch(Exception ex)
                        {
                            throw ex;
                        }
                    }
                    else throw new Exception(String.Format("Cannot execute {0}", current));

                }
                catch (Exception ex)
                {
                    pd.Error(ex);
                    logger.Error("Error when running script", ex);
                    var progress = pd.Progress<Defines.TransferProgress>();
                    if (progress != null)
                        progress.Report(Defines.TransferProgress.Error(ex));
                    throw ex;
                }

            }
        }

    }
}
