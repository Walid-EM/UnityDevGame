using UnityEngine;
using System.Collections.Generic;

// Définition des types d'items
public enum ItemType
{
    Weapon,
    Consumable,
    Resource,
    Equipment
    // Ajoutez d'autres types selon vos besoins
}

// ===============================================================
// Base de données centralisée des items
// ===============================================================

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Inventory/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemData> items = new List<ItemData>();
    
    private Dictionary<int, ItemData> itemDictionary = new Dictionary<int, ItemData>();
    private static ItemDatabase _instance;
    
    // Accès singleton à la base de données
    public static ItemDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<ItemDatabase>("ItemDatabase");
                if (_instance == null)
                {
                    Debug.LogError("ItemDatabase non trouvée dans les Resources!");
                    return null;
                }
                _instance.Initialize();
            }
            return _instance;
        }
    }
    
    public void Initialize()
    {
        itemDictionary.Clear();
        foreach (var item in items)
        {
            if (!itemDictionary.ContainsKey(item.ID))
                itemDictionary.Add(item.ID, item);
            else
                Debug.LogWarning($"Item avec ID {item.ID} dupliqué dans la base de données!");
        }
        Debug.Log($"Base de données d'items initialisée avec {items.Count} items");
    }
    
    public ItemData GetItem(int id)
    {
        if (itemDictionary.Count == 0)
            Initialize();
            
        if (itemDictionary.TryGetValue(id, out ItemData item))
            return item;
            
        Debug.LogWarning($"Item avec ID {id} non trouvé dans la base de données");
        return null;
    }
    
    public ItemData GetItemByName(string name)
    {
        if (itemDictionary.Count == 0)
            Initialize();
            
        foreach (var item in items)
        {
            if (item.Name == name)
                return item;
        }
        
        Debug.LogWarning($"Item avec nom {name} non trouvé dans la base de données");
        return null;
    }
    
    public List<ItemData> GetAllItems()
    {
        return items;
    }
}

// ===============================================================
// Définition des données d'un item
// ===============================================================

[System.Serializable]
public class ItemData
{
    public int ID;
    public string Name;
    public Sprite InventoryIcon;
    public GameObject WorldPrefab;
    public GameObject InventoryPrefab; // Si différent du WorldPrefab
    
    public ItemType Type;
    public string Description;
    public bool IsStackable;
    public int MaxStackSize = 1;
    public int BaseValue = 1; // Valeur de base de l'item
    
    // Propriétés spécifiques à certains types d'items
    [Header("Propriétés spécifiques")]
    public float WeaponDamage; // Pour les armes
    public float HealthRestore; // Pour les consommables de type santé
    public float ManaRestore; // Pour les consommables de type mana
    public float HungerRestore; // Pour les consommables de type nourriture
    
    // Constructeur vide
    public ItemData() { }
    
    // Constructeur de copie
    public ItemData(ItemData source)
    {
        if (source != null)
        {
            this.ID = source.ID;
            this.Name = source.Name;
            this.InventoryIcon = source.InventoryIcon;
            this.WorldPrefab = source.WorldPrefab;
            this.InventoryPrefab = source.InventoryPrefab;
            this.Type = source.Type;
            this.Description = source.Description;
            this.IsStackable = source.IsStackable;
            this.MaxStackSize = source.MaxStackSize;
            this.BaseValue = source.BaseValue;
            this.WeaponDamage = source.WeaponDamage;
            this.HealthRestore = source.HealthRestore;
            this.ManaRestore = source.ManaRestore;
            this.HungerRestore = source.HungerRestore;
        }
    }
    
    // Récupérer le prefab à utiliser pour l'équipement
    public GameObject GetEquipPrefab()
    {
        return InventoryPrefab != null ? InventoryPrefab : WorldPrefab;
    }
}

// ===============================================================
// Données d'une instance d'item
// ===============================================================

[System.Serializable]
public class ItemInstance
{
    // ID faisant référence à l'ItemData dans la base de données
    public int itemID;
    
    // Propriétés spécifiques à l'instance
    public int quantity = 1;
    public string uniqueID = "";
    
    // Cache des données
    private ItemData _data;
    public ItemData Data 
    { 
        get 
        {
            if (_data == null)
                _data = ItemDatabase.Instance.GetItem(itemID);
            return _data;
        }
    }
    
    // Accesseurs pratiques
    public string Name => Data?.Name ?? "Item inconnu";
    public Sprite Icon => Data?.InventoryIcon;
    public string Description => Data?.Description ?? "";
    public bool IsStackable => Data?.IsStackable ?? false;
    public ItemType Type => Data?.Type ?? ItemType.Resource;
    public GameObject EquipPrefab => Data?.GetEquipPrefab();
    public bool CanBeEquipped => Type == ItemType.Weapon || Type == ItemType.Equipment;
    
    // Constructeur par défaut
    public ItemInstance() { }
    
    // Constructeur à partir d'un ID
    public ItemInstance(int id, int quantity = 1)
    {
        this.itemID = id;
        this.quantity = quantity;
        
        // Générer un ID unique si nécessaire
        if (!IsStackable)
            this.uniqueID = System.Guid.NewGuid().ToString();
    }
    
    // Constructeur de copie
    public ItemInstance(ItemInstance source)
    {
        if (source != null)
        {
            this.itemID = source.itemID;
            this.quantity = source.quantity;
            
            // Si l'item n'est pas empilable, générer un nouvel ID unique
            if (!source.IsStackable)
                this.uniqueID = System.Guid.NewGuid().ToString();
            else
                this.uniqueID = source.uniqueID;
        }
    }
    
    // Méthode pour actualiser les données
    public void RefreshData()
    {
        _data = null; // Force la récupération des données à nouveau
    }
}