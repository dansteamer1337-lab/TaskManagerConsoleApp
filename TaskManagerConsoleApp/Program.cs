using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;

namespace TaskManagerConsoleApp
{
    // Перечисление уровней ошибок
    public enum ErrorLevel
    {
        Info,
        Warning,
        Error,
        Fatal,
        Debug
    }

    // Класс для хранения информации об ошибке
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
            Timestamp = DateTime.Now;
            AdditionalData = new Dictionary<string, object>();
        }
    }

    // Централизованный обработчик ошибок
    public static class ExceptionHandler
    {
        private static readonly string errorLogPath = "error_log.txt";

        // Событие для оповещения о серьезных ошибках
        public static event Action<ErrorContext> OnSeriousError;

        static ExceptionHandler()
        {
            // Подписываемся на событие для отправки уведомлений
            OnSeriousError += NotifySeriousError;
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
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            };

            // Логируем ошибку
            LogError(errorContext);

            // Выводим в консоль
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n!!! Ошибка в операции '{operation}' !!!");
            Console.WriteLine($"Сообщение: {ex.Message}");
            Console.ResetColor();

            // Для фатальных ошибок показываем стек вызовов
            if (level == ErrorLevel.Fatal)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Стек вызовов:\n{ex.StackTrace}");
                Console.ResetColor();
            }

            // Вызываем событие для серьезных ошибок
            if (level == ErrorLevel.Error || level == ErrorLevel.Fatal)
            {
                OnSeriousError?.Invoke(errorContext);
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
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            };

            LogError(errorContext);

            Console.ForegroundColor = level == ErrorLevel.Fatal ? ConsoleColor.DarkRed : ConsoleColor.Yellow;
            Console.WriteLine($"\n!!! {message} !!!");
            Console.ResetColor();

            if (level == ErrorLevel.Error || level == ErrorLevel.Fatal)
            {
                OnSeriousError?.Invoke(errorContext);
            }
        }

        private static void LogError(ErrorContext error)
        {
            // Логирование в файл
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

            // Логирование через Serilog
            switch (error.Level)
            {
                case ErrorLevel.Error:
                    Log.Error(error.Exception, "Ошибка в операции {Operation}: {Message}", error.Operation, error.Message);
                    break;
                case ErrorLevel.Fatal:
                    Log.Fatal(error.Exception, "Фатальная ошибка в операции {Operation}: {Message}", error.Operation, error.Message);
                    break;
                case ErrorLevel.Warning:
                    Log.Warning("Предупреждение в операции {Operation}: {Message}", error.Operation, error.Message);
                    break;
                default:
                    Log.Information("Информация об ошибке в операции {Operation}: {Message}", error.Operation, error.Message);
                    break;
            }
        }

        private static void NotifySeriousError(ErrorContext error)
        {
            // Оповещение в консоль
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"\n!!! КРИТИЧЕСКАЯ ОШИБКА !!! Уровень: {error.Level}");
            Console.WriteLine($"Пожалуйста, проверьте лог-файл: {errorLogPath}");
            Console.ResetColor();

            // Отправка в Sentry (закомментировано, требует настройки)
            // SentrySdk.CaptureException(error.Exception);

            // Отправка email (пример)
            // SendEmailNotification(error);

            // В реальном проекте здесь можно добавить отправку в Application Insights
            // TelemetryClient.TrackException(error.Exception);
        }

        // Пример отправки email уведомления
        private static void SendEmailNotification(ErrorContext error)
        {
            // Здесь код для отправки email
            // Это заглушка для примера
            Log.Information("Отправлено email-уведомление об ошибке: {Message}", error.Message);
        }
    }

    public class TaskItem
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsCompleted { get; set; }

        public override string ToString()
        {
            string status = IsCompleted ? "[X]" : "[ ]";
            return $"{Id}. {status} {Description} (Создано: {CreatedAt:dd.MM.yyyy HH:mm})";
        }
    }

    class Program
    {
        private static List<TaskItem> tasks = new List<TaskItem>();
        private static int nextId = 1;
        private static readonly string logFilePath = "app_log.txt";

        static void Main(string[] args)
        {
            try
            {
                // Настройка Serilog
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .WriteTo.File("logs\\myapp-.log",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                Log.Debug("Приложение запущено.");

                // Централизованная обработка необработанных исключений
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    var ex = (Exception)e.ExceptionObject;
                    ExceptionHandler.HandleException(ex, "UnhandledException", ErrorLevel.Fatal,
                        new Dictionary<string, object> { { "IsTerminating", e.IsTerminating } });
                };

                // Обработка исключений в потоках
                System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
                {
                    ExceptionHandler.HandleException(e.Exception, "UnobservedTaskException", ErrorLevel.Error);
                    e.SetObserved();
                };

                bool exit = false;
                while (!exit)
                {
                    try
                    {
                        PrintMenu();
                        string input = Console.ReadLine();
                        Log.Information($"Пользователь ввел команду: {input}");

                        switch (input)
                        {
                            case "1":
                                CreateTask();
                                break;
                            case "2":
                                DeleteTask();
                                break;
                            case "3":
                                ViewTasks();
                                break;
                            case "4":
                                MarkTaskAsCompleted();
                                break;
                            case "5":
                                exit = true;
                                Log.Information("Пользователь завершил работу приложения.");
                                Console.WriteLine("До свидания!");
                                break;
                            default:
                                ExceptionHandler.HandleNonExceptionError(
                                    $"Неверная команда: {input}",
                                    "MainMenu",
                                    ErrorLevel.Warning);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler.HandleException(ex, "MainLoop", ErrorLevel.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "ApplicationStartup", ErrorLevel.Fatal);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        static void PrintMenu()
        {
            Console.WriteLine("\n--- Менеджер задач ---");
            Console.WriteLine("1. Создать задачу");
            Console.WriteLine("2. Удалить задачу");
            Console.WriteLine("3. Показать все задачи");
            Console.WriteLine("4. Отметить задачу как выполненную");
            Console.WriteLine("5. Выход");
            Console.Write("Выберите действие: ");
        }

        static void CreateTask()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                Log.Information("Start Create Task");

                Console.Write("Введите описание задачи: ");
                string description = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(description))
                {
                    ExceptionHandler.HandleNonExceptionError(
                        "Описание задачи не может быть пустым",
                        "CreateTask",
                        ErrorLevel.Warning,
                        new Dictionary<string, object> { { "Description", description } });
                    return;
                }

                var newTask = new TaskItem
                {
                    Id = nextId++,
                    Description = description,
                    CreatedAt = DateTime.Now,
                    IsCompleted = false
                };

                tasks.Add(newTask);

                Console.WriteLine($"Задача '{description}' успешно добавлена (ID: {newTask.Id})!");
                Log.Information($"Задача успешно создана с ID: {newTask.Id}");

                stopwatch.Stop();
                Log.Information($"Close Create Task | ВРЕМЯ: {stopwatch.ElapsedMilliseconds} мс | ID: {newTask.Id}");
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "CreateTask", ErrorLevel.Error);
            }
        }

        static void DeleteTask()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                Log.Information("Start Delete Task");

                if (tasks.Count == 0)
                {
                    ExceptionHandler.HandleNonExceptionError(
                        "Нет задач для удаления",
                        "DeleteTask",
                        ErrorLevel.Warning);
                    return;
                }

                ViewTasks();

                Console.Write("Введите ID задачи для удаления: ");
                if (!int.TryParse(Console.ReadLine(), out int id))
                {
                    ExceptionHandler.HandleNonExceptionError(
                        "Некорректный ID задачи",
                        "DeleteTask",
                        ErrorLevel.Warning,
                        new Dictionary<string, object> { { "Input", id.ToString() } });
                    return;
                }

                Log.Information($"Попытка удалить задачу с ID: {id}");

                var taskToDelete = tasks.FirstOrDefault(t => t.Id == id);
                if (taskToDelete != null)
                {
                    tasks.Remove(taskToDelete);
                    Console.WriteLine($"Задача '{taskToDelete.Description}' удалена.");
                    Log.Information($"Задача с ID {id} успешно удалена.");
                }
                else
                {
                    ExceptionHandler.HandleNonExceptionError(
                        $"Задача с ID {id} не найдена",
                        "DeleteTask",
                        ErrorLevel.Error,
                        new Dictionary<string, object> { { "TaskId", id } });
                }

                stopwatch.Stop();
                Log.Information($"Close Delete Task | ВРЕМЯ: {stopwatch.ElapsedMilliseconds} мс");
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "DeleteTask", ErrorLevel.Error);
            }
        }

        static void ViewTasks()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                Log.Information("Start View Task");

                Console.WriteLine("\n--- Список задач ---");

                if (tasks.Count == 0)
                {
                    Console.WriteLine("Задач пока нет.");
                    Log.Information("Просмотр списка задач: список пуст.");
                }
                else
                {
                    foreach (var task in tasks)
                    {
                        Console.WriteLine(task.ToString());
                    }
                    Log.Information($"Просмотр списка задач. Всего задач: {tasks.Count}");
                }

                stopwatch.Stop();
                Log.Information($"Close View Task | ВРЕМЯ: {stopwatch.ElapsedMilliseconds} мс | Всего задач: {tasks.Count}");
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "ViewTasks", ErrorLevel.Error);
            }
        }

        static void MarkTaskAsCompleted()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                Log.Information("Start Mark Task As Completed");

                if (tasks.Count == 0)
                {
                    ExceptionHandler.HandleNonExceptionError(
                        "Нет задач для отметки",
                        "MarkTaskAsCompleted",
                        ErrorLevel.Warning);
                    return;
                }

                ViewTasks();

                Console.Write("Введите ID задачи для отметки как выполненной: ");
                if (!int.TryParse(Console.ReadLine(), out int id))
                {
                    ExceptionHandler.HandleNonExceptionError(
                        "Некорректный ID задачи",
                        "MarkTaskAsCompleted",
                        ErrorLevel.Warning,
                        new Dictionary<string, object> { { "Input", id.ToString() } });
                    return;
                }

                Log.Information($"Попытка отметить задачу с ID {id} как выполненную.");

                var task = tasks.FirstOrDefault(t => t.Id == id);
                if (task != null)
                {
                    if (!task.IsCompleted)
                    {
                        task.IsCompleted = true;
                        Console.WriteLine($"Задача '{task.Description}' отмечена как выполненная!");
                        Log.Information($"Задача с ID {id} отмечена как выполненная.");
                    }
                    else
                    {
                        ExceptionHandler.HandleNonExceptionError(
                            $"Задача с ID {id} уже была выполнена ранее",
                            "MarkTaskAsCompleted",
                            ErrorLevel.Warning,
                            new Dictionary<string, object> { { "TaskId", id } });
                    }
                }
                else
                {
                    ExceptionHandler.HandleNonExceptionError(
                        $"Задача с ID {id} не найдена",
                        "MarkTaskAsCompleted",
                        ErrorLevel.Error,
                        new Dictionary<string, object> { { "TaskId", id } });
                }

                stopwatch.Stop();
                Log.Information($"Close Mark Task As Completed | ВРЕМЯ: {stopwatch.ElapsedMilliseconds} мс");
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "MarkTaskAsCompleted", ErrorLevel.Error);
            }
        }
    }
}