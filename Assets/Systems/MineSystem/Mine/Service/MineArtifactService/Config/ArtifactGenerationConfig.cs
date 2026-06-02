using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Config
{
    [CreateAssetMenu(fileName = "ArtifactGenerationConfig", menuName = "Config/ArtifactGenerationConfig")]
    public class ArtifactGenerationConfig : ScriptableObject
    {
        public Region region;
        public Site site;
        
        public int minNumberOfArtifacts;
        public int maxNumberOfArtifacts;

        public int minNumberOfRareArtifacts;
        public int maxNumberOfRareArtifacts;
        
        public int minNumberOfLegendaryArtifacts;
        public int maxNumberOfLegendaryArtifacts;

        public List<ArtifactGenerationData> artifactGenerationDatas;
    }
}

/*
check out the resource generationservice script how it has been used in the minegenerationmodel script and do similar thing for artifactgenerationservice script.

#region Generate Artifacts

	private async Task GenerateArtifacts(Mine mine)
	{
		var rawArtifactFunctionals = _rawArtifactDto.RawArtifactFunctionals;
		
		var siteArtifactChance = GetSiteArtifactChanceDataBySite(_mineGenerationDto.ProceduralMineGenerationData.Site);
		
		var regionalArtifacts = rawArtifactFunctionals!
			.Where(rawArtifactFunctional => rawArtifactFunctional.Region == _mineGenerationDto.ProceduralMineGenerationData.Region).ToList();
		
		var weaponCount = (int) (siteArtifactChance.Weapon * _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		var armorCount = (int) (siteArtifactChance.Armor * _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		var clothingCount = (int) (siteArtifactChance.Clothing * _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		var economicCount = (int) (siteArtifactChance.Economic * _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		var vesselCount = (int) (siteArtifactChance.Vessel * _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		var leisureCount = (int) (siteArtifactChance.Leisure * _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		var toolCount = (int) (siteArtifactChance.Tool * _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		var ceremonialCount = (int) (siteArtifactChance.Ceremonial * _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		var legendaryCount = (int) (siteArtifactChance.Legendary * _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		
		var listOfRawArtifacts = new List<RawArtifactFunctional>();
	
		listOfRawArtifacts.AddRange(regionalArtifacts.Where(rawArtifact => rawArtifact.ObjectClass == "Weapon").ToList()
			.OrderBy(_ => _rand.Next()).Take(weaponCount));
		listOfRawArtifacts.AddRange(regionalArtifacts.Where(rawArtifact => rawArtifact.ObjectClass == "Armor").ToList()
			.OrderBy(_ => _rand.Next()).Take(armorCount));
		listOfRawArtifacts.AddRange(regionalArtifacts.Where(rawArtifact => rawArtifact.ObjectClass == "Clothing").ToList()
			.OrderBy(_ => _rand.Next()).Take(clothingCount));
		listOfRawArtifacts.AddRange(regionalArtifacts.Where(rawArtifact => rawArtifact.ObjectClass == "Economic").ToList()
			.OrderBy(_ => _rand.Next()).Take(economicCount));
		listOfRawArtifacts.AddRange(regionalArtifacts.Where(rawArtifact => rawArtifact.ObjectClass == "Vessel").ToList()
			.OrderBy(_ => _rand.Next()).Take(vesselCount));
		listOfRawArtifacts.AddRange(regionalArtifacts.Where(rawArtifact => rawArtifact.ObjectClass == "Leisure").ToList()
			.OrderBy(_ => _rand.Next()).Take(leisureCount));
		listOfRawArtifacts.AddRange(regionalArtifacts.Where(rawArtifact => rawArtifact.ObjectClass == "Tool").ToList()
			.OrderBy(_ => _rand.Next()).Take(toolCount));
		listOfRawArtifacts.AddRange(regionalArtifacts.Where(rawArtifact => rawArtifact.ObjectClass == "Ceremonial").ToList()
			.OrderBy(_ => _rand.Next()).Take(ceremonialCount));
		listOfRawArtifacts.AddRange(regionalArtifacts.Where(rawArtifact => rawArtifact.ObjectClass == "Legendary").ToList()
			.OrderBy(_ => _rand.Next()).Take(legendaryCount));
		
		GD.Print($"raw artifact functional: {rawArtifactFunctionals.Count}");
		GD.Print($"list of raw artifacts: {listOfRawArtifacts.Count}");
	
		#region Adding duplicate artifacts in the list of artifacts
	
		if (listOfRawArtifacts.Count < _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts)
		{
			var duplicateArtifacts = new List<RawArtifactFunctional>();
			var duplicateCounter = _mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts - listOfRawArtifacts.Count;
			
			for (var i = 0; i < duplicateCounter; i++)
			{
				var rawArtifact = listOfRawArtifacts[_rand.Next(0, listOfRawArtifacts.Count)];
				duplicateArtifacts.Add(rawArtifact);
			}
			
			listOfRawArtifacts.AddRange(duplicateArtifacts);
		}
	
		#endregion
	
		#region Adding Condition and Rarity to the generated artifacts
	
		var listOfArtifactRarityConditions = GetConditionRarityCombination(_mineGenerationDto.ProceduralMineGenerationData.TotalNoOfArtifacts);
		var rarityConditionCounter = 0;
		var listOfArtifacts = new List<Artifact>();
		
		foreach (var artifactFunctional in listOfRawArtifacts)
		{
			var rarityCondition = listOfArtifactRarityConditions[rarityConditionCounter];
			var artifact = new Artifact
			{
				Id = Guid.NewGuid().ToString(),
				RawArtifactId = artifactFunctional.Id,
				Condition = rarityCondition.Item1.Condition,
				Rarity = rarityCondition.Item2.Rarity
			};
	
			rarityConditionCounter++;
			listOfArtifacts.Add(artifact);
			GD.Print($"{rarityCondition.Item1.Condition}, {rarityCondition.Item2.Rarity}");
		}
	
		#endregion
		
		var cells = mine.Cells.ToList();
		var cellsToRemove = new List<Cell>();
		foreach (var cell in cells)
		{
			if (cell.IsBroken || cell.HasCave || !cell.IsInstantiated || !cell.IsBreakable) 
				cellsToRemove.Add(cell);
		}
	
		#region Tutorial Tiles
	
		var midPoint = _mineGenerationDto.ProceduralMineGenerationData.MineSizeX / 2;
		for (var i = midPoint -1; i < midPoint +1; i++)
		{
			for (var j = 1; j < 3; j++)
			{
				if(i == midPoint && j == 2) continue;
				cellsToRemove.Add(cells.FirstOrDefault(tempCell=> tempCell.PositionX == i && tempCell.PositionY == j));
			}
		}
	
		#endregion
		
		foreach (var cell in cellsToRemove)
		{
			cells.Remove(cell);
		}
	
		foreach (var artifact in listOfArtifacts)
		{
			var cell = cells[_rand.Next(0, cells.Count)];
			artifact.PositionX = cell.PositionX;
			artifact.PositionY = cell.PositionY;
			cells.Remove(cell);
		}
	
		var tutorialCell = cells.FirstOrDefault(tempCell => tempCell is { PositionX: 24, PositionY: 2 });
		if (tutorialCell is { HasArtifact: false })
		{
			var tutorialArtifact = new Artifact
			{
				Id = "tutorialArtifact",
				RawArtifactId = "ClassicalNativeAmericanTomahawk",
				PositionX = 24,
				PositionY = 2,
				Condition = "Decrepit",
				Rarity = "Common",
				Slot = 0
			};
				
			listOfArtifacts.Add(tutorialArtifact);
		}
		
		_rawArtifactDto.Artifacts = listOfArtifacts.ToList();
		AssignArtifactsToMine(listOfArtifacts, mine);
		GD.Print("ARTIFACTS IN DTO");
		foreach (var artifact in _rawArtifactDto.Artifacts)
		{
			GD.Print($"DTO Artifacts: {artifact.Id} === {artifact.PositionX}, {artifact.PositionY}");
		}
		
		GD.Print("ARTIFACTS IN MINE");
		foreach (var mineCell in mine.Cells)
		{
			if(!mineCell.HasArtifact) continue;
			GD.Print($"Artifact: {mineCell.ArtifactId} ||| {mineCell.PositionX}, {mineCell.PositionY}");
		}

		await Task.Delay(500);
	}

	private List<Tuple<ArtifactCondition, ArtifactRarity>> GetConditionRarityCombination(int artifactCount)
	{
		var artifactConditions = _mineGenerationDto.ArtifactConditions;
		var artifactRarities = _mineGenerationDto.ArtifactRarities;
		var conditionsRarityList = new List<Tuple<ArtifactCondition, ArtifactRarity>>();
		
		GD.Print($"artifact conditions count {artifactConditions.Count}");

		foreach (var condition in artifactConditions)
			GD.Print($"conditions is {condition.Condition}");
		
		foreach (var rarity in artifactRarities)
			GD.Print($"conditions is {rarity.Rarity}");

		for (var i = 0; i < artifactCount; i++)
		{
			var conditionValue = _rand.Next(0, 101);
			var rarityValue = _rand.Next(0, 101);

			var condition = conditionValue switch
			{
				<=75 => artifactConditions[0],
				> 75 and <= 98 => artifactConditions[1],
				> 98 => artifactConditions[2]
			};

			var rarity = rarityValue switch
			{
				<= 80 => artifactRarities[0],
				> 80 and <= 99 => artifactRarities[1],
				> 99 => artifactRarities[2]
			};
			
			var tuple = new Tuple<ArtifactCondition, ArtifactRarity>(condition, rarity);
			conditionsRarityList.Add(tuple);
		}

		return conditionsRarityList;
	}

	private SiteArtifactChanceData GetSiteArtifactChanceDataBySite(string site)
	{
		var siteChanceData = _mineGenerationDto.SiteArtifactChances.FirstOrDefault(temp => temp.Site == site);
		if (siteChanceData == null)
		{
			GD.PrintErr($"Fatal Error: Site does not match the database");
			return null;
		}

		GD.Print($"site chance data: legendary- {siteChanceData.Legendary}");
		return siteChanceData;
	}

	private void AssignArtifactsToMine(List<Artifact> artifacts, Mine mine)
	{
		foreach (var artifact in artifacts)
		{
			var cell = mine.Cells.FirstOrDefault(tempCell =>
				tempCell.PositionX == artifact.PositionX && tempCell.PositionY == artifact.PositionY);
			if (cell == null)
			{
				GD.PrintErr("Fatal Error: artifact position does not match any of the cell positions");
				continue;
			}
				
			var rawArtifactFunctional =
				_rawArtifactDto.RawArtifactFunctionals.FirstOrDefault(temp => temp.Id == artifact.RawArtifactId);
			if (rawArtifactFunctional == null)
			{
				GD.PrintErr("Fatal Error: Artifact rawArtifactId does not match any RawArtifactFunctionalId");
				continue;
			}
				
			cell.HasArtifact = true;
			cell.ArtifactId = artifact.Id;
			var mat = rawArtifactFunctional.Materials[0];
			cell.ArtifactMaterial = mat;
		}
	}

	#endregion

this is the core code implementation for artifact generation, however the method generateArtifact will use unitask with parameters minedata and ArtifactGenerationConfig. use the values from ArtifactGenerationConfig to generate the amounts mentioned.
*/