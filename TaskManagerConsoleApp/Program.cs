using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace TaskManagerConsoleApp
{

    public enum TaskPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public TaskPriority Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsCompleted { get; set; }

        public override string ToString()
        {
            string status = IsCompleted ? "[✓]" : "[ ]";
            string prioritySymbol = Priority switch
            {
                TaskPriority.Low => "↓",
                TaskPriority.Medium => "●",
                TaskPriority.High => "↑",
                TaskPriority.Critical => "!!!",
                _ => "?"
            };

            return $"{Id}. {status} [{prioritySymbol}] {Title} - {Description} (Создано: {CreatedAt:dd.MM.yyyy HH:mm})";
        }
    }


    public static class LoggerManager
    {
        public static ILogger Logger { get; private set; }
        private static Logger _serilogLogger;

        static LoggerManager()
        {
            ConfigureLogger();
        }

        private static void ConfigureLogger()
        {
            if (!Directory.Exists("logs"))
                Directory.CreateDirectory("logs");

            _serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/taskmanager-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Logger = _serilogLogger;
        }

        public static void LogTaskOperation(string operation, TaskItem task, string result)
        {
            Logger.Information("Task operation: {Operation}, Task ID: {TaskId}, Title: {TaskTitle}, Result: {Result}",
                operation, task.Id, task.Title, result);
        }

        public static void LogPerformance(string operation, long elapsedMilliseconds, string details = null)
        {
            Logger.Debug("Performance: {Operation} completed in {ElapsedMilliseconds} ms {Details}",
                operation, elapsedMilliseconds, details ?? "");
        }

        public static void CloseAndFlush()
        {
            _serilogLogger?.Dispose();
            Logger = null;
        }
    }


    public class TraceManager : IDisposable
    {
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private readonly string _details;
        private bool _isDisposed;

        public TraceManager(string operationName, string details = null)
        {
            _operationName = operationName;
            _details = details;
            _stopwatch = Stopwatch.StartNew();

            Trace.TraceInformation($"[START] {operationName} at {DateTime.Now:HH:mm:ss.fff}");
            LoggerManager.Logger.Debug("Trace: Starting operation {Operation} {Details}", operationName, details);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _stopwatch.Stop();
                Trace.TraceInformation($"[STOP] {_operationName} completed in {_stopwatch.ElapsedMilliseconds} ms");

                LoggerManager.Logger.Debug("Trace: Operation {Operation} completed in {ElapsedMilliseconds} ms {Details}",
                    _operationName, _stopwatch.ElapsedMilliseconds, _details);

                LoggerManager.LogPerformance(_operationName, _stopwatch.ElapsedMilliseconds, _details);
                _isDisposed = true;
            }
        }
    }


    public class TaskService
    {
        private readonly List<TaskItem> _tasks = new();
        private int _nextId = 1;

        public void AddTask(string title, string description = "", TaskPriority priority = TaskPriority.Medium)
        {
            using var trace = new TraceManager(nameof(AddTask), $"Title: {title}");

            try
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    throw new ArgumentException("Название задачи не может быть пустым", nameof(title));
                }

                var task = new TaskItem
                {
                    Id = _nextId++,
                    Title = title.Trim(),
                    Description = description?.Trim() ?? "",
                    Priority = priority,
                    CreatedAt = DateTime.Now,
                    IsCompleted = false
                };

                _tasks.Add(task);

                LoggerManager.LogTaskOperation("ADD", task, "Success");
                LoggerManager.Logger.Information("Task added: ID {TaskId}, Title {TaskTitle}", task.Id, task.Title);

                Console.WriteLine($"Задача \"{task.Title}\" успешно добавлена (ID: {task.Id})");
            }
            catch (Exception ex)
            {
                LoggerManager.Logger.Error(ex, "Ошибка при добавлении задачи: {Title}", title);
                Console.WriteLine($"Ошибка при добавлении задачи: {ex.Message}");
                throw;
            }
        }

        public bool RemoveTask(int id)
        {
            using var trace = new TraceManager(nameof(RemoveTask), $"ID: {id}");

            try
            {
                var task = _tasks.FirstOrDefault(t => t.Id == id);
                if (task == null)
                {
                    LoggerManager.Logger.Warning("Попытка удалить несуществующую задачу: ID {TaskId}", id);
                    Console.WriteLine($"Задача с ID {id} не найдена");
                    return false;
                }

                _tasks.Remove(task);
                LoggerManager.LogTaskOperation("REMOVE", task, "Success");
                LoggerManager.Logger.Information("Task removed: ID {TaskId}, Title {TaskTitle}", task.Id, task.Title);

                Console.WriteLine($"Задача \"{task.Title}\" удалена");
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Logger.Error(ex, "Ошибка при удалении задачи ID: {TaskId}", id);
                Console.WriteLine($"Ошибка при удалении задачи: {ex.Message}");
                throw;
            }
        }

        public List<TaskItem> GetAllTasks()
        {
            using var trace = new TraceManager(nameof(GetAllTasks), $"Total: {_tasks.Count}");

            try
            {
                var tasksList = _tasks.OrderBy(t => t.Priority).ThenByDescending(t => t.CreatedAt).ToList();

                LoggerManager.Logger.Information("Отображен список задач: {Count} задач", tasksList.Count);
                LoggerManager.Logger.Debug("Tasks retrieved: {Count} tasks", tasksList.Count);

                return tasksList;
            }
            catch (Exception ex)
            {
                LoggerManager.Logger.Error(ex, "Ошибка при получении списка задач");
                Console.WriteLine($"Ошибка при получении списка задач: {ex.Message}");
                throw;
            }
        }

        public bool CompleteTask(int id)
        {
            using var trace = new TraceManager(nameof(CompleteTask), $"ID: {id}");

            try
            {
                var task = _tasks.FirstOrDefault(t => t.Id == id);
                if (task == null)
                {
                    LoggerManager.Logger.Warning("Попытка отметить несуществующую задачу: ID {TaskId}", id);
                    Console.WriteLine($"Задача с ID {id} не найдена");
                    return false;
                }

                if (task.IsCompleted)
                {
                    LoggerManager.Logger.Warning("Попытка повторно отметить задачу: ID {TaskId}, Title {TaskTitle}", task.Id, task.Title);
                    Console.WriteLine($"Задача \"{task.Title}\" уже была выполнена");
                    return false;
                }

                task.IsCompleted = true;
                LoggerManager.LogTaskOperation("COMPLETE", task, "Success");
                LoggerManager.Logger.Information("Task completed: ID {TaskId}, Title {TaskTitle}", task.Id, task.Title);

                Console.WriteLine($"✓ Задача \"{task.Title}\" отмечена как выполненная");
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Logger.Error(ex, "Ошибка при отметке задачи ID: {TaskId}", id);
                Console.WriteLine($"Ошибка при отметке задачи: {ex.Message}");
                throw;
            }
        }

        public void DisplayTasks()
        {
            using var trace = new TraceManager(nameof(DisplayTasks));

            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("СПИСОК ЗАДАЧ");
            Console.WriteLine(new string('=', 60));

            var tasks = GetAllTasks();

            if (!tasks.Any())
            {
                Console.WriteLine(" Задач пока нет");
                LoggerManager.Logger.Information("Empty task list displayed");
                return;
            }

            foreach (var task in tasks)
            {
                Console.WriteLine($"  {task}");
            }

            Console.WriteLine(new string('-', 60));
            Console.WriteLine($"Всего: {tasks.Count} | Выполнено: {tasks.Count(t => t.IsCompleted)} | Активных: {tasks.Count(t => !t.IsCompleted)}");
            Console.WriteLine(new string('=', 60));
        }
    }


    public static class ExceptionHandler
    {
        private static readonly string errorLogPath = "logs/error_log.txt";

        static ExceptionHandler()
        {
            if (!Directory.Exists("logs"))
                Directory.CreateDirectory("logs");
        }

        public static void HandleException(Exception ex, string operation, ErrorLevel level = ErrorLevel.Error,
            Dictionary<string, object> additionalData = null)
        {
            var errorContext = new ErrorContext
            {
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                Operation = operation,
                Level = level,
                Exception = ex,
                Timestamp = DateTime.Now,
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            };

            LogError(errorContext);

            Console.ForegroundColor = level == ErrorLevel.Fatal ? ConsoleColor.DarkRed : ConsoleColor.Red;
            Console.WriteLine($"\n!!! Ошибка в операции '{operation}' !!!");
            Console.WriteLine($"Сообщение: {ex.Message}");
            Console.ResetColor();

            if (level == ErrorLevel.Fatal)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Стек вызовов:\n{ex.StackTrace}");
                Console.ResetColor();
            }
        }

        public static void HandleNonExceptionError(string message, string operation, ErrorLevel level = ErrorLevel.Error,
            Dictionary<string, object> additionalData = null)
        {
            var errorContext = new ErrorContext
            {
                Message = message,
                StackTrace = Environment.StackTrace,
                Operation = operation,
                Level = level,
                Timestamp = DateTime.Now,
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            };

            LogError(errorContext);

            Console.ForegroundColor = level == ErrorLevel.Fatal ? ConsoleColor.DarkRed : ConsoleColor.Yellow;
            Console.WriteLine($"\n!!! {message} !!!");
            Console.ResetColor();
        }

        private static void LogError(ErrorContext error)
        {
            string logEntry = $"[{error.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{error.Level}] " +
                             $"Операция: {error.Operation}\n" +
                             $"Сообщение: {error.Message}\n" +
                             $"Стек вызовов: {error.StackTrace}\n";

            if (error.AdditionalData.Any())
            {
                logEntry += "Дополнительные данные:\n";
                foreach (var data in error.AdditionalData)
                {
                    logEntry += $"  {data.Key}: {data.Value}\n";
                }
            }

            logEntry += new string('-', 80) + "\n";

            File.AppendAllText(errorLogPath, logEntry);

            switch (error.Level)
            {
                case ErrorLevel.Error:
                    LoggerManager.Logger.Error(error.Exception, "Ошибка в операции {Operation}: {Message}", error.Operation, error.Message);
                    break;
                case ErrorLevel.Fatal:
                    LoggerManager.Logger.Fatal(error.Exception, "Фатальная ошибка в операции {Operation}: {Message}", error.Operation, error.Message);
                    break;
                case ErrorLevel.Warning:
                    LoggerManager.Logger.Warning("Предупреждение в операции {Operation}: {Message}", error.Operation, error.Message);
                    break;
                default:
                    LoggerManager.Logger.Information("Информация об ошибке в операции {Operation}: {Message}", error.Operation, error.Message);
                    break;
            }
        }
    }

    public enum ErrorLevel
    {
        Info,
        Warning,
        Error,
        Fatal,
        Debug
    }

    public class ErrorContext
    {
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string Operation { get; set; }
        public ErrorLevel Level { get; set; }
        public DateTime Timestamp { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; }

        public ErrorContext()
        {
            AdditionalData = new Dictionary<string, object>();
        }
    }


    class Program
    {
        private static TaskService _taskService;
        private static bool _exitRequested = false;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = (Exception)e.ExceptionObject;
                if (LoggerManager.Logger != null)
                    LoggerManager.Logger.Fatal(ex, "НЕОБРАБОТАННОЕ ИСКЛЮЧЕНИЕ - Приложение будет закрыто");

                Console.WriteLine("\n!!! КРИТИЧЕСКАЯ ОШИБКА !!!");
                Console.WriteLine($"Пожалуйста, проверьте лог-файлы в папке 'logs'");
                Console.WriteLine($"Ошибка: {ex.Message}");

                if (Debugger.IsAttached)
                {
                    Console.WriteLine($"Стек вызовов:\n{ex.StackTrace}");
                }
            };

            try
            {
                InitializeApplication();
                RunMainLoop();
            }
            catch (Exception ex)
            {
                if (LoggerManager.Logger != null)
                    LoggerManager.Logger.Fatal(ex, "Фатальная ошибка при запуске приложения");

                Console.WriteLine($"\n!!! КРИТИЧЕСКАЯ ОШИБКА ПРИ ЗАПУСКЕ: {ex.Message} !!!");
                Console.WriteLine("Пожалуйста, проверьте конфигурацию и повторите попытку.");
            }
            finally
            {
                ShutdownApplication();
            }
        }

        private static void InitializeApplication()
        {
            LoggerManager.Logger.Information("=== ЗАПУСК ПРИЛОЖЕНИЯ TaskManager ===");
            LoggerManager.Logger.Information("Операционная система: {OS}", Environment.OSVersion);
            LoggerManager.Logger.Information("Версия .NET: {Version}", Environment.Version);

            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.AutoFlush = true;
            Trace.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Application started");

            _taskService = new TaskService();

            LoggerManager.Logger.Information("TaskService инициализирован");
            Console.WriteLine("Добро пожаловать в TaskManager!");
            Console.WriteLine("=".PadRight(50, '='));
        }

        private static void RunMainLoop()
        {
            while (!_exitRequested)
            {
                try
                {
                    DisplayMenu();
                    var input = Console.ReadLine();

                    LoggerManager.Logger.Information("Пользователь ввел команду: {Command}", input);

                    ProcessCommand(input);
                }
                catch (Exception ex)
                {
                    ExceptionHandler.HandleException(ex, "MainLoop", ErrorLevel.Error);
                    Console.WriteLine("\n!!! Ошибка при обработке команды !!!");
                    Console.WriteLine("Проверьте корректность ввода и попробуйте снова.");
                    Console.WriteLine("Подробности в лог-файлах.\n");
                }
            }
        }

        private static void DisplayMenu()
        {
            Console.WriteLine("\n" + new string('-', 50));
            Console.WriteLine("ГЛАВНОЕ МЕНЮ");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("1. Добавить задачу");
            Console.WriteLine("2. Удалить задачу");
            Console.WriteLine("3. Показать все задачи");
            Console.WriteLine("4. Отметить задачу как выполненную");
            Console.WriteLine("5. Выход");
            Console.WriteLine(new string('-', 50));
            Console.Write("Ваш выбор: ");
        }

        private static void ProcessCommand(string input)
        {
            using var trace = new TraceManager(nameof(ProcessCommand), $"Command: {input}");

            switch (input)
            {
                case "1":
                    AddTaskFlow();
                    break;
                case "2":
                    RemoveTaskFlow();
                    break;
                case "3":
                    _taskService.DisplayTasks();
                    break;
                case "4":
                    CompleteTaskFlow();
                    break;
                case "5":
                    _exitRequested = true;
                    LoggerManager.Logger.Information("Пользователь инициировал завершение работы");
                    Console.WriteLine("\nДо свидания!");
                    break;
                default:
                    ExceptionHandler.HandleNonExceptionError(
                        $"Неверная команда: {input}",
                        "ProcessCommand",
                        ErrorLevel.Warning);
                    Console.WriteLine("Неверная команда. Пожалуйста, выберите 1-5.");
                    break;
            }
        }

        private static void AddTaskFlow()
        {
            using var trace = new TraceManager(nameof(AddTaskFlow));

            try
            {
                Console.Write("Название задачи: ");
                var title = Console.ReadLine();

                Console.Write("Описание (опционально, Enter для пропуска): ");
                var description = Console.ReadLine();

                Console.Write("Приоритет (0-Low, 1-Medium, 2-High, 3-Critical) [1]: ");
                var priorityInput = Console.ReadLine();
                var priority = priorityInput switch
                {
                    "0" => TaskPriority.Low,
                    "2" => TaskPriority.High,
                    "3" => TaskPriority.Critical,
                    _ => TaskPriority.Medium
                };

                _taskService.AddTask(title, description, priority);
            }
            catch (ArgumentException ex)
            {
                ExceptionHandler.HandleException(ex, "AddTaskFlow", ErrorLevel.Warning);
                Console.WriteLine($"{ex.Message}");
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "AddTaskFlow", ErrorLevel.Error);
                Console.WriteLine("Произошла ошибка при добавлении задачи. Проверьте логи.");
            }
        }

        private static void RemoveTaskFlow()
        {
            using var trace = new TraceManager(nameof(RemoveTaskFlow));

            try
            {
                _taskService.DisplayTasks();

                if (_taskService.GetAllTasks().Count > 0)
                {
                    Console.Write("\nВведите ID задачи для удаления: ");
                    if (int.TryParse(Console.ReadLine(), out int id))
                    {
                        _taskService.RemoveTask(id);
                    }
                    else
                    {
                        ExceptionHandler.HandleNonExceptionError(
                            $"Введен некорректный ID",
                            "RemoveTaskFlow",
                            ErrorLevel.Warning);
                        Console.WriteLine("Пожалуйста, введите корректный числовой ID.");
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "RemoveTaskFlow", ErrorLevel.Error);
                Console.WriteLine("Произошла ошибка при удалении задачи. Проверьте логи.");
            }
        }

        private static void CompleteTaskFlow()
        {
            using var trace = new TraceManager(nameof(CompleteTaskFlow));

            try
            {
                _taskService.DisplayTasks();
                if (_taskService.GetAllTasks().Count > 0)
                {
                    Console.Write("\nВведите ID задачи для отметки как выполненной: ");
                    if (int.TryParse(Console.ReadLine(), out int id))
                    {
                        _taskService.CompleteTask(id);
                    }
                    else
                    {
                        ExceptionHandler.HandleNonExceptionError(
                            $"Введен некорректный ID",
                            "CompleteTaskFlow",
                            ErrorLevel.Warning);
                        Console.WriteLine("Пожалуйста, введите корректный числовой ID.");
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "CompleteTaskFlow", ErrorLevel.Error);
                Console.WriteLine("Произошла ошибка при отметке задачи. Проверьте логи.");
            }
        }

        private static void ShutdownApplication()
        {
            if (LoggerManager.Logger != null)
            {
                LoggerManager.Logger.Information("=== ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ TaskManager ===");
                Trace.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Application shutdown");
                Trace.Flush();

                LoggerManager.CloseAndFlush();
            }

            Console.WriteLine("\nЛог-файлы сохранены в папке 'logs'");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}