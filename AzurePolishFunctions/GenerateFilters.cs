using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using FilterEconomy.Facades;
using FilterCore;
using FilterEconomyProcessor;
using System.Linq;
using System.Collections.Generic;
using FilterPolishUtil.Collections;
using FilterEconomy.Model;

namespace AzurePolishFunctions
{
    public static class GenerateFilters
    {
        public static EconomyRequestFacade EconomyData { get; set; }
        public static ItemInformationFacade ItemInfoData { get; set; }
        public static TierListFacade TierListFacade { get; set; }
        public static FilterAccessFacade FilterAccessFacade { get; set; } = FilterAccessFacade.GetInstance();

        [FunctionName("GenerateFilters")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Admin, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            return name != null
                ? (ActionResult)new OkObjectResult($"Hello, {name}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }

        public static void PerformMainRoutine()
        {
            // 0) Establish Logging, Facades

            // 0) Get Current League information etc
            // 1) Acquire Data
            // 2) Test Data
            // 3) Initialize static enrichment information
            // 4) Parse filter, Load All files (Economy, Basetype, Tierlist) -> All facade

            //FilterAccessFacade.PrimaryFilter = PerformFilterWork();

            EconomyData.EnrichAll(EnrichmentProcedureConfiguration.EnrichmentProcedures);
            TierListFacade.TierListData.Values.ToList().ForEach(x => x.ReEvaluate());

            // 5) Generate Suggestions 

            var economyTieringSystem = new ConcreteEconomyRules();
            economyTieringSystem.GenerateSuggestions();
            TierListFacade.TierListData.Values.ToList().ForEach(x => x.ReEvaluate());

            // 6) Apply suggestions

            TierListFacade.ApplyAllSuggestions();

            // 7) Generate Filters
            // 8) Generate changelogs
            // 9) Upload filters
        }

        private static void CreateSubEconomyTiers()
        {
            var shaperbases = new Dictionary<string, ItemList<FilterEconomy.Model.NinjaItem>>();
            var elderbases = new Dictionary<string, ItemList<FilterEconomy.Model.NinjaItem>>();
            var otherbases = new Dictionary<string, ItemList<FilterEconomy.Model.NinjaItem>>();

            foreach (var items in EconomyData.EconomyTierlistOverview["basetypes"])
            {
                var shapergroup = items.Value.Where(x => x.Variant == "Shaper").ToList();
                if (shapergroup.Count != 0)
                {
                    shaperbases.Add(items.Key, new ItemList<NinjaItem>());
                    shaperbases[items.Key].AddRange((shapergroup));
                }

                var eldegroup = items.Value.Where(x => x.Variant == "Elder").ToList();
                if (eldegroup.Count != 0)
                {
                    elderbases.Add(items.Key, new ItemList<NinjaItem>());
                    elderbases[items.Key].AddRange((eldegroup));
                }

                var othergroup = items.Value.Where(x => x.Variant != "Shaper" && x.Variant != "Elder").ToList();
                if (othergroup.Count != 0)
                {
                    otherbases.Add(items.Key, new ItemList<NinjaItem>());
                    otherbases[items.Key].AddRange((othergroup));
                }
            }

            EconomyData.AddToDictionary("rare->shaper", shaperbases);
            EconomyData.AddToDictionary("rare->elder", elderbases);
            EconomyData.AddToDictionary("rare->normal", otherbases);
        }
    }
}
