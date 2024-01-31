using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScrapWorker.Managers
{
    internal class ConsoleManager
    {
        internal delegate void WriteColored(IColorMessage message);
        internal interface IColorMessage
        {
            public event WriteColored? OnWrite;
            public void ForegroundColor(ConsoleColor color);
            public ConsoleColor? Color { get; }
            public object? Message { get; }

            public void WriteLine(object? message);
        }

        internal sealed class ColorMessage : IColorMessage
        {
            public ConsoleColor? Color { get; private set; }

            public object? Message { get; private set; }

            public event WriteColored? OnWrite;

            public void ForegroundColor(ConsoleColor color) => Color = color;

            public void WriteLine(object? message)
            {
                Message = message;
                OnWrite!.Invoke(this);

                Message = null;
                Color = null;
            }
        }

        internal sealed class ErrorMessage : IColorMessage
        {
            public ConsoleColor? Color => ConsoleColor.Red;

            public object? Message { get; private set; }

            public event WriteColored? OnWrite;

            public void ForegroundColor(ConsoleColor color) { }

            public void WriteLine(object message)
            {
                Message = message;
                OnWrite!.Invoke(this);

                Message = null;
            }
        }

        private readonly ConcurrentQueue<object?> MessageQueue = new();
        private readonly Task LoggingTask;
        private readonly CancellationToken Token;
        private bool IsWaitingForExit = false;

        public readonly IColorMessage Colored;
        public readonly IColorMessage Error;
        public ConsoleManager(CancellationToken tok)
        {
            Token = tok;
            LoggingTask = new Task(DoConsoleLog, Token, TaskCreationOptions.LongRunning);

            Colored = new ColorMessage();
            Colored.OnWrite += ColoredWrite;

            Error = new ErrorMessage();
            Error.OnWrite += ColoredWrite;
        }

        private void ColoredWrite(IColorMessage message)
        {
            MessageQueue.Enqueue(message);
        }

        public void WriteLine(object? message)
        {
            MessageQueue.Enqueue(message);
        }

        public void WriteLine(params object[] message)
        {
            foreach (var obj in message)
                MessageQueue.Enqueue(obj);
        }

        private async void DoConsoleLog()
        {
            while (!Token.IsCancellationRequested || !IsWaitingForExit)
            {
                foreach (var msg in MessageQueue)
                {
                    switch (msg)
                    {
                        case IColorMessage cmsg:
                            if (cmsg.Color.HasValue) Console.ForegroundColor = cmsg.Color.Value;
                            Console.WriteLine(cmsg.Message);
                            Console.ResetColor();
                            break;
                        default:
                            Console.WriteLine(msg);
                            break;
                    }
                }

                await Task.Delay(50);
            }
        }

        public void StartOutput()
        {
            LoggingTask.Start();
        }

        public void WaitForExit()
        {
            IsWaitingForExit = true;
            LoggingTask.Wait();
        }
    }
}
