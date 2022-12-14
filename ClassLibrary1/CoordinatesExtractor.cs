using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using DesignAutomationFramework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace CoordinatesExtractor
{
	[Regeneration(RegenerationOption.Manual)]
	[Transaction(TransactionMode.Manual)]
	public class CoordinatesExtractor : IExternalDBApplication
	{
		public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
		{
			return ExternalDBApplicationResult.Succeeded;
		}

		public ExternalDBApplicationResult OnStartup(ControlledApplication application)
		{
			DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
			return ExternalDBApplicationResult.Succeeded;
		}

		private void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
		{
			e.Succeeded = true;
			ExtractModelCoordinates(e.DesignAutomationData);
		}

		private void ExtractModelCoordinates(DesignAutomationData designAutomationData)
		{
			if (designAutomationData == null) throw new ArgumentNullException(nameof(designAutomationData));

			Application rvtApp = designAutomationData.RevitApp;
			if (rvtApp == null) throw new InvalidDataException(nameof(rvtApp));

			string modelPath = designAutomationData.FilePath;
			if (String.IsNullOrWhiteSpace(modelPath)) throw new InvalidDataException(nameof(modelPath));

			Document doc = designAutomationData.RevitDoc;
			if (doc == null) throw new InvalidOperationException("Could not open document.");

			List<Coordinates> coordinates = GetCoordinates("testacciona.csv");

			InputParams inputParameters = JsonConvert.DeserializeObject<InputParams>(File.ReadAllText("params.json"));

			dynamic urnResult = new JObject();
			try
			{
				//urnResult.projectBasePoint = BasePoint.GetProjectBasePoint(doc);
				BasePoint pbp = BasePoint.GetProjectBasePoint(doc);
				Console.WriteLine($"Project base point acquired!");
				//urnResult.surveyPoint = BasePoint.GetSurveyPoint(doc);
				BasePoint sp = BasePoint.GetSurveyPoint(doc);
				Console.WriteLine($"Survey point acquired!");
				//urnResult.angleToTrueNorth = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero).Angle;
				double pp = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero).Angle;
				Console.WriteLine($"True north angle acquired!");

				try
				{
					Coordinates correctCoordinates = coordinates.Find(c => c.basePoint.);
				}
				catch (Exception)
				{

					throw;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

			// save all to a .json file
			using (StreamWriter file = File.CreateText("result.json"))
			using (JsonTextWriter writer = new JsonTextWriter(file))
			{
				urnResult.WriteTo(writer);
			}
		}

		private static List<Coordinates> GetCoordinates(string fileName)
		{
			List<Coordinates> coordinates = new List<Coordinates>();
			using (var reader = new StreamReader(fileName))
			{
				while (!reader.EndOfStream)
				{
					var line = reader.ReadLine();
					var values = line.Split(',');

					XYZ projectBasePoint = new XYZ(
						double.Parse(values[1]),
						double.Parse(values[2]),
						double.Parse(values[3])
					);

					XYZ surveyPoint = new XYZ(
						double.Parse(values[5]),
						double.Parse(values[6]),
						double.Parse(values[7])
					);

					coordinates.Add(new Coordinates()
					{
						code = values[0],
						angle = double.Parse(values[4]),
						basePoint = projectBasePoint,
						surveyPoint = surveyPoint
					});
				}
			}
			return coordinates;
		}
	}

	internal class InputParams
	{
		public string userId { get; set; }
		public string versionUrn { get; set; }
		public string projectId { get; set; }
	}

	public class Coordinates
	{
		public XYZ basePoint { get; set; }
		public XYZ surveyPoint { get; set; }
		public double angle { get; set; }
		public string code { get; set; }
	}
}
