using Xunit;
using TaskManagerConsoleApp;
using System.Linq;

namespace TaskManagerConsoleApp.Tests
{
    public class TaskServiceTests
    {
        [Fact]
        public void AddTask_ShouldAddNewTaskToList()
        {
            var service = new TaskService();

            service.AddTask("Купить кофе", "Срочно нужно для кода", TaskPriority.High);
            var tasks = service.GetAllTasks();

            Assert.Single(tasks);
            Assert.Equal("Купить кофе", tasks.First().Title);
            Assert.Equal(TaskPriority.High, tasks.First().Priority);
        }

        [Fact]
        public void CompleteTask_ShouldChangeStatusToCompleted()
        {

            var service = new TaskService();
            service.AddTask("Задача для выполнения");
            var taskId = service.GetAllTasks().First().Id;

            var result = service.CompleteTask(taskId);
            var task = service.GetAllTasks().First();

            Assert.True(result);
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void RemoveTask_ShouldDeleteTaskFromList()
        {
            var service = new TaskService();
            service.AddTask("Задача на удаление");
            var taskId = service.GetAllTasks().First().Id;

            var result = service.RemoveTask(taskId);
            var tasks = service.GetAllTasks();

            Assert.True(result);
            Assert.Empty(tasks);
        }
    }
}