using Microsoft.Extensions.Options;
using MPI;
using Faker;
using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        int i = 0, world, rank, localX, localY;
        // Инициализация MPI.NET
        MPI.Environment.Run(ref args, comm =>
        {
            Stopwatch stopwatch = new Stopwatch();
            rank = comm.Rank;       // Запись ранга процесса
            world = comm.Size;      // Запись общего числа процессов

            // Процесс ранга 0 взаимодействует с консолью
            if (comm.Rank == 0)
            {
                bool Flag = false;
                while (Flag == false)
                {
                    Console.WriteLine("\nВведите число экземпляров, которое создаст каждый процесс");
                    try
                    {
                        i = Convert.ToInt32(Console.ReadLine());
                        Flag = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Некорректный ввод. Попробуйте ещё раз");
                    }
                }
                Console.WriteLine($"Всего будет добавлено {world*i} экземпляров");
            }

            // Рассылка значения i всем процессам и назначение процесс ранга 0 рассылателем (root)
            comm.Broadcast(ref i, 0);

            // Создание экземпляра класса MyDbContext
            using (var context = new MyDbContext())
            {
                // Создание базы процессом ранга 0, если она ещё не была создана 
                if (rank == 0)
                {
                    int a = 0;
                    bool Flag = false;
                    while (Flag == false)
                    {
                        Console.WriteLine("Если требуется пересоздать БД, то введите 1");
                        try
                        {
                            a = Convert.ToInt32(Console.ReadLine());
                            Flag = true;
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Некорректный ввод. Попробуйте ещё раз");
                        }
                    }
                    if (a == 1)
                    {
                        context.Database.EnsureDeleted();
                        Console.WriteLine("Предыдущая БД была удалена");
                    }
                    context.Database.EnsureCreated();
                    stopwatch.Start();  // Запуск таймера
                }

                // Функция формирует барьер, и ни один процесс в коммуникаторе не может преодолеть барьер,
                // пока все они не вызовут функцию
                comm.Barrier();

                // Добавление данных в таблицу
                for (int j = 0; j < i; j++)
                {
                    // Генерирование случайных данных
                    context.Users.Add(new User { Name = Name.First(), SecondName = Name.Last(), Salary = RandomNumber.Next(1000000000) });
                }
                // Сохранение изменений в БД
                context.SaveChanges();
            }

            // Функция формирует барьер, и ни один процесс в коммуникаторе не может преодолеть барьер,
            // пока все они не вызовут функцию
            comm.Barrier();

            // Создание экземпляра класса MyDbContext
            using (var context = new MyDbContext())
            {
                // Разбиение БД на части
                int dataSize = context.Users.Count();
                int chunkSize = dataSize / world;
                int startIndex = rank * chunkSize;
                int endIndex = (rank + 1) * chunkSize;
                if (rank == world - 1)
                {
                    endIndex = dataSize;
                }
                localX = 1000000000;
                localY = 0;

                // Запрос на получение части БД
               var users = context.Users
                        .OrderBy(x => x.Id)
                        .Skip(startIndex)
                        .Take(endIndex - startIndex)
                        .ToList();

                // Поиск наименьшего и наибольшего значения в этой части
                foreach (var user in users)
                {
                    if (localX > user.Salary)
                        localX = user.Salary;
                    if (localY > user.Salary)
                        localY = user.Salary;
                }

                // Сбор значений из процессов и поиск минимального/минимального
                comm.Reduce(localX, Math.Min, 0);
                comm.Reduce(localY, Math.Min, 0);

                if (rank == 0)
                {
                    // Вывод  минимального значения процессом ранга 0 
                    Console.WriteLine($"Минимальная зарплата = {localX} рублей, максимальная = {localY}");
                    stopwatch.Start();
                    Console.WriteLine("Прошло времени:" + stopwatch.ElapsedMilliseconds);
                }
            }
        });
    }
}