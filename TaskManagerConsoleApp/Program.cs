using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TaskManagerConsoleApp
{
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
        private static readonly string traceFilePath = "trace_log.txt";

        static void Main(string[] args)
        {
            SetupTracing();

            Trace.TraceInformation("Приложение запущено.");
            LogInfo("Приложение запущено.");

            bool exit = false;
            while (!exit)
            {
                PrintMenu();
                string input = Console.ReadLine();
                Trace.TraceInformation($"Пользователь ввел команду: {input}");

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
                        Trace.TraceInformation("Пользователь завершил работу приложения.");
                        LogInfo("Приложение завершено.");
                        Console.WriteLine("До свидания!");
                        break;
                    default:
                        Console.WriteLine("Неверная команда. Попробуйте снова.");
                        Trace.TraceWarning($"Введена неверная команда: {input}");
                        break;
                }
            }

            Trace.Close();
        }

        static void SetupTracing()
        {
            Trace.Listeners.Clear();

            Trace.Listeners.Add(new ConsoleTraceListener());

            TextWriterTraceListener fileListener = new TextWriterTraceListener(traceFilePath);
            Trace.Listeners.Add(fileListener);

            Trace.AutoFlush = true;

            Trace.WriteLine("=== Новая сессия трассировки ===");
        }
        static void LogInfo(string message)
        {
            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] {message}";
            File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
        }

        static void LogError(string message, Exception ex = null)
        {
            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] {message}";
            if (ex != null)
                logMessage += $" | Exception: {ex.Message}";

            File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
            Trace.TraceError(message);
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
            Console.Write("Введите описание задачи: ");
            string description = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(description))
            {
                Console.WriteLine("Описание не может быть пустым.");
                Trace.TraceWarning("Попытка создать задачу с пустым описанием.");
                LogError("Попытка создать задачу с пустым описанием.");
                return;
            }

            Trace.TraceInformation($"Начало создания задачи с описанием: {description}");

            var newTask = new TaskItem
            {
                Id = nextId++,
                Description = description,
                CreatedAt = DateTime.Now,
                IsCompleted = false
            };

            tasks.Add(newTask);

            Trace.TraceInformation($"Задача успешно создана с ID: {newTask.Id}");
            LogInfo($"Создана задача: ID={newTask.Id}, Описание={description}");

            Console.WriteLine($"Задача '{description}' успешно добавлена (ID: {newTask.Id})!");
        }

        static void DeleteTask()
        {
            ViewTasks();

            if (tasks.Count == 0)
            {
                Console.WriteLine("Нет задач для удаления.");
                return;
            }

            Console.Write("Введите ID задачи для удаления: ");
            if (!int.TryParse(Console.ReadLine(), out int id))
            {
                Console.WriteLine("Некорректный ID.");
                Trace.TraceWarning("Введен некорректный ID для удаления.");
                LogError("Некорректный ID при попытке удаления.");
                return;
            }

            Trace.TraceInformation($"Попытка удалить задачу с ID: {id}");

            var taskToDelete = tasks.FirstOrDefault(t => t.Id == id);
            if (taskToDelete != null)
            {
                tasks.Remove(taskToDelete);
                Console.WriteLine($"Задача '{taskToDelete.Description}' удалена.");

                Trace.TraceInformation($"Задача с ID {id} успешно удалена.");
                LogInfo($"Удалена задача: ID={id}, Описание={taskToDelete.Description}");
            }
            else
            {
                Console.WriteLine($"Задача с ID {id} не найдена.");
                Trace.TraceWarning($"Задача с ID {id} не найдена для удаления.");
                LogError($"Попытка удалить несуществующую задачу с ID {id}");
            }
        }

        static void ViewTasks()
        {
            Console.WriteLine("\n--- Список задач ---");

            if (tasks.Count == 0)
            {
                Console.WriteLine("Задач пока нет.");
                Trace.TraceInformation("Просмотр списка задач: список пуст.");
                LogInfo("Просмотр пустого списка задач.");
                return;
            }

            foreach (var task in tasks)
            {
                Console.WriteLine(task.ToString());
            }

            Trace.TraceInformation($"Просмотр списка задач. Всего задач: {tasks.Count}");
            LogInfo($"Просмотр списка задач. Всего: {tasks.Count}");
        }

        static void MarkTaskAsCompleted()
        {
            ViewTasks();

            if (tasks.Count == 0)
            {
                Console.WriteLine("Нет задач для отметки.");
                return;
            }

            Console.Write("Введите ID задачи для отметки как выполненной: ");
            if (!int.TryParse(Console.ReadLine(), out int id))
            {
                Console.WriteLine("Некорректный ID.");
                Trace.TraceWarning("Введен некорректный ID для отметки задачи.");
                LogError("Некорректный ID при отметке задачи как выполненной.");
                return;
            }

            Trace.TraceInformation($"Попытка отметить задачу с ID {id} как выполненную.");

            var task = tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                if (!task.IsCompleted)
                {
                    task.IsCompleted = true;
                    Console.WriteLine($"Задача '{task.Description}' отмечена как выполненная!");

                    Trace.TraceInformation($"Задача с ID {id} отмечена как выполненная.");
                    LogInfo($"Задача выполнена: ID={id}, Описание={task.Description}");
                }
                else
                {
                    Console.WriteLine("Эта задача уже была выполнена ранее.");
                    Trace.TraceWarning($"Попытка повторно отметить задачу ID {id} как выполненную.");
                }
            }
            else
            {
                Console.WriteLine($"Задача с ID {id} не найдена.");
                Trace.TraceWarning($"Задача с ID {id} не найдена для отметки.");
                LogError($"Попытка отметить несуществующую задачу с ID {id}");
            }
        }
    }
}