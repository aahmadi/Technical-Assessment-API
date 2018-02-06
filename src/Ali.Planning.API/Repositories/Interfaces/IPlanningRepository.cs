
using Ali.Planning.API.Model;
using Entities;
using System.Linq;

namespace Ali.Planning.API.Repositories
{
    public interface IPlanningRepository:
        IRepository<ProjectPlanning>
    {
        IQueryable<PlanModel> GetEmployeePlansWithProjectName(int employeeId);
    }
}
