using UnityEngine;
using System.IO;
using System.Text;

public class ExportCarPhysicsData : MonoBehaviour
{
    [ContextMenu("Export Car Physics Values")]
    void ExportValues()
    {
        SCR_RayBasedCarPhysics car = GetComponent<SCR_RayBasedCarPhysics>();
        if (car == null) return;
        
        StringBuilder sb = new StringBuilder();
        
        // Reflection para pegar todos os serialized fields
        var fields = typeof(SCR_RayBasedCarPhysics).GetFields(
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance | 
            System.Reflection.BindingFlags.Public
        );
        
        foreach (var field in fields)
        {
            var serialized = System.Attribute.GetCustomAttribute(field, typeof(SerializeField));
            if (serialized != null || field.IsPublic)
            {
                try
                {
                    object value = field.GetValue(car);
                    if (value != null && !field.FieldType.IsClass && !field.FieldType.IsArray)
                    {
                        sb.AppendLine($"{field.Name} = {value}");
                    }
                }
                catch { }
            }
        }
        
        File.WriteAllText(Application.dataPath + "/CarPhysicsExport.txt", sb.ToString());
        Debug.Log("Exportado para: " + Application.dataPath + "/CarPhysicsExport.txt");
    }
}