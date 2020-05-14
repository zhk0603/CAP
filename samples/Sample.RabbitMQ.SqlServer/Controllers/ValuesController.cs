using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Sample.RabbitMQ.SqlServer.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private readonly ICapPublisher _capBus;
        private readonly IConfiguration _configuration;

        public ValuesController(ICapPublisher capPublisher,IConfiguration configuration)
        {
            _capBus = capPublisher;
            _configuration = configuration;
        }

        [Route("~/without/transaction")]
        public async Task<IActionResult> WithoutTransaction()
        {
            await _capBus.PublishAsync("sample.rabbitmq.mysql", new Person()
            {
                Id = 123,
                Name = "Bar"
            });

            return Ok();
        }

        [Route("~/adonet/transaction")]
        public IActionResult AdonetWithTransaction()
        {
            using (var connection = new SqlConnection(_configuration["DefaultConnectionString"]))
            {
                using (var transaction = connection.BeginTransaction(_capBus, true))
                {
                    //your business code
                    connection.Execute("insert into test(name) values('test')", transaction: transaction);

                    _capBus.Publish("sample.rabbitmq.mysql", DateTime.Now);
                }
            }

            return Ok();
        }

        [Route("~/ef/transaction")]
        public IActionResult EntityFrameworkWithTransaction([FromServices]AppDbContext dbContext)
        {
            using (dbContext.Database.BeginTransaction(_capBus, autoCommit: true))
            {
                dbContext.Persons.Add(new Person() { Name = "ef.transaction" });
                dbContext.SaveChanges();
                _capBus.Publish("sample.rabbitmq.mysql", DateTime.Now);
            }
            return Ok();
        }

        [NonAction]
        [CapSubscribe("sample.rabbitmq.mysql")]
        public void Subscriber(DateTime p)
        {
            Console.WriteLine($@"{DateTime.Now} Subscriber invoked, Info: {p}");
        }

        [NonAction]
        [CapSubscribe("sample.rabbitmq.mysql")]
        public void Subscriberv3(DateTime p)
        {
            Console.WriteLine($@"V3 {DateTime.Now} Subscriber invoked, Info: {p}");
        }

        [NonAction]
        [CapSubscribe("sample.rabbitmq.mysql", Group = "group.test2")]
        public void Subscriber2(DateTime p, [FromCap]CapHeader header)
        {
            Console.WriteLine($@"{DateTime.Now} Subscriber invoked, Info: {p}");
        }
    }
}
