﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CriticalCommonLib.Enums;
using CriticalCommonLib.Models;
using Dalamud.Logging;
using Dalamud.Plugin;
using InventoryTools.Comparers;

namespace InventoryTools.Logic
{
    public static class FilterManager
    {
        public static FilterResult GenerateFilteredList(FilterConfiguration filter, Dictionary<ulong, Dictionary<InventoryCategory, List<InventoryItem>>> inventories)
        {
            var sortedItems = new List<SortingResult>();
            var unsortableItems = new List<InventoryItem>();
            var activeCharacter = PluginLogic.CharacterMonitor.ActiveCharacter;
            var activeRetainer = PluginLogic.CharacterMonitor.ActiveRetainer;
            var displayCrossCharacter = PluginLogic.PluginConfiguration.DisplayCrossCharacter;
            
            PluginLog.Verbose("Generating a new filter list");

            if (filter.FilterType == FilterType.SortingFilter)
            {
                //Determine which source and destination inventories we actually need to examine
                Dictionary<(ulong, InventoryCategory), List<InventoryItem>> sourceInventories = new();
                Dictionary<(ulong, InventoryCategory), List<InventoryItem>> destinationInventories = new();
                foreach (var character in inventories)
                {
                    foreach (var inventory in character.Value)
                    {
                        var inventoryKey = (character.Key, inventory.Key);
                        if (filter.SourceAllRetainers.HasValue && filter.SourceAllRetainers.Value && PluginLogic.CharacterMonitor.IsRetainer(character.Key) && (displayCrossCharacter || PluginLogic.CharacterMonitor.BelongsToActiveCharacter(character.Key)))
                        {
                            if (!sourceInventories.ContainsKey(inventoryKey))
                            {
                                sourceInventories.Add(inventoryKey, inventory.Value);
                            }
                        }
                        else if (filter.SourceAllCharacters.HasValue && filter.SourceAllCharacters.Value &&
                                 PluginLogic.CharacterMonitor.ActiveCharacter == character.Key && (displayCrossCharacter || PluginLogic.CharacterMonitor.BelongsToActiveCharacter(character.Key)))
                        {
                            if (!sourceInventories.ContainsKey(inventoryKey))
                            {
                                sourceInventories.Add(inventoryKey, inventory.Value);
                            }
                        }
                        else if (filter.SourceInventories.Contains(inventoryKey) && (displayCrossCharacter || PluginLogic.CharacterMonitor.BelongsToActiveCharacter(character.Key)))
                        {

                            if (!sourceInventories.ContainsKey(inventoryKey))
                            {
                                sourceInventories.Add(inventoryKey, inventory.Value);
                            }
                        }

                        if (filter.DestinationAllRetainers.HasValue && filter.DestinationAllRetainers.Value && PluginLogic.CharacterMonitor.IsRetainer(character.Key) && (displayCrossCharacter || PluginLogic.CharacterMonitor.BelongsToActiveCharacter(character.Key)))
                        {
                            if (!destinationInventories.ContainsKey(inventoryKey))
                            {
                                destinationInventories.Add(inventoryKey, inventory.Value);
                            }
                        }
                        else if (filter.DestinationAllCharacters.HasValue && filter.DestinationAllCharacters.Value &&
                                 PluginLogic.CharacterMonitor.ActiveCharacter == character.Key && (displayCrossCharacter || PluginLogic.CharacterMonitor.BelongsToActiveCharacter(character.Key)))
                        {
                            if (!destinationInventories.ContainsKey(inventoryKey))
                            {
                                destinationInventories.Add(inventoryKey, inventory.Value);
                            }
                        }
                        else if (filter.DestinationInventories.Contains(inventoryKey) && (displayCrossCharacter || PluginLogic.CharacterMonitor.BelongsToActiveCharacter(character.Key)))
                        {
                            if (!destinationInventories.ContainsKey(inventoryKey))
                            {
                                destinationInventories.Add(inventoryKey, inventory.Value);
                            }
                        }
                    }
                }

                //Filter the source and destination inventories based on the applicable items so we have less to sort
                Dictionary<(ulong, InventoryCategory), List<InventoryItem>> filteredSources = new();
                //Dictionary<(ulong, InventoryCategory), List<InventoryItem>> filteredDestinations = new();
                var sourceKeys = sourceInventories.Select(c => c.Key);
                PluginLog.Verbose(sourceInventories.Count() + " inventories to examine.");
                foreach (var sourceInventory in sourceInventories)
                {
                    if (!filteredSources.ContainsKey(sourceInventory.Key))
                    {
                        filteredSources.Add(sourceInventory.Key, new List<InventoryItem>());
                    }

                    filteredSources[sourceInventory.Key].AddRange(sourceInventory.Value.Where(filter.FilterItem));
                }

                var slotsAvailable = new Dictionary<(ulong, InventoryCategory), int>();
                var itemLocations = new Dictionary<int, List<InventoryItem>>();
                var absoluteItemLocations = new Dictionary<int, HashSet<(ulong, InventoryCategory)>>();
                foreach (var destinationInventory in destinationInventories)
                {
                    foreach (var destinationItem in destinationInventory.Value)
                    {
                        if (!slotsAvailable.ContainsKey(destinationInventory.Key))
                        {
                            slotsAvailable.Add(destinationInventory.Key, 0);
                        }

                        destinationItem.TempQuantity = destinationItem.Quantity;
                        if (filter.FilterItem(destinationItem))
                        {
                            var itemHashCode = destinationItem.GetHashCode();
                            if (!itemLocations.ContainsKey(itemHashCode))
                            {
                                itemLocations.Add(itemHashCode, new List<InventoryItem>());
                            }

                            itemLocations[itemHashCode].Add(destinationItem);                     
                        }
                        else if (destinationItem.IsEmpty)
                        {
                            slotsAvailable[destinationInventory.Key] = slotsAvailable[destinationInventory.Key] + 1;
                        }
                    }
                }

                foreach (var sourceInventory in filteredSources)
                {
                    for (var index = 0; index < sourceInventory.Value.Count; index++)
                    {
                        var sourceItem = sourceInventory.Value[index];
                        sourceItem.TempQuantity = sourceItem.Quantity;
                        //Item already seen, try to put it into that container
                        var hashCode = sourceItem.GetHashCode();
                        if (itemLocations.ContainsKey(hashCode))
                        {
                            for (var i = 0; i < itemLocations[hashCode].Count; i++)
                            {
                                var existingItem = itemLocations[hashCode][i];
                                //Don't compare inventory to itself
                                if (existingItem.RetainerId == sourceItem.RetainerId)
                                {
                                    continue;
                                }

                                if (!existingItem.FullStack)
                                {
                                    var existingCapacity = existingItem.RemainingTempStack;
                                    var canFit = Math.Min(existingCapacity, sourceItem.TempQuantity);
                                    if (canFit != 0)
                                    {
                                        PluginLog.Verbose("Existing item has a capacity of " + existingCapacity +
                                                          " and can fit " + canFit);
                                        PluginLog.Verbose("Existing item has a stack size of " +
                                                          (existingItem.Item == null
                                                              ? "unknown"
                                                              : existingItem.Item.StackSize) + " and has quantity of " +
                                                          existingItem.TempQuantity);
                                        //All the item can fit, stick it in and continue
                                        if (filter.InActiveInventories(activeCharacter, activeRetainer,
                                            sourceInventory.Key.Item1, existingItem.RetainerId))
                                        {
                                            PluginLog.Verbose("Added item to filter result in existing slot: " +
                                                              sourceItem.FormattedName);
                                            sortedItems.Add(new SortingResult(sourceInventory.Key.Item1,
                                                existingItem.RetainerId, sourceItem.SortedContainer,
                                                existingItem.SortedCategory, sourceItem, (int) canFit));
                                        }

                                        if (!absoluteItemLocations.ContainsKey(hashCode))
                                        {
                                            absoluteItemLocations.Add(hashCode,
                                                new HashSet<(ulong, InventoryCategory)>());
                                        }

                                        absoluteItemLocations[hashCode]
                                            .Add((existingItem.RetainerId, existingItem.SortedCategory));
                                        existingItem.TempQuantity += canFit;
                                        sourceItem.TempQuantity -= canFit;
                                    }
                                }
                                else
                                {
                                    PluginLog.Verbose("Existing item does have a full stack");
                                    if (!absoluteItemLocations.ContainsKey(hashCode))
                                    {
                                        absoluteItemLocations.Add(hashCode, new HashSet<(ulong, InventoryCategory)>());
                                    }

                                    absoluteItemLocations[hashCode]
                                        .Add((existingItem.RetainerId, existingItem.SortedCategory));
                                }

                                if (sourceItem.TempQuantity == 0)
                                {
                                    break;
                                }
                            }


                            //The item is empty, continue, otherwise work out what inventory we should try to place it in
                            if (sourceItem.TempQuantity == 0)
                            {
                                continue;
                            }
                            else
                            {
                                if (absoluteItemLocations.ContainsKey(hashCode))
                                {
                                    var seenInventoryLocations = absoluteItemLocations[hashCode];
                                    while (seenInventoryLocations.Count != 0)
                                    {
                                        var seenInventoryLocation = seenInventoryLocations.First();
                                        if (slotsAvailable.ContainsKey(seenInventoryLocation))
                                        {
                                            var slotCount = slotsAvailable[seenInventoryLocation];
                                            if (slotCount != 0)
                                            {
                                                slotsAvailable[seenInventoryLocation] =
                                                    slotsAvailable[seenInventoryLocation] - 1;
                                                if (!sourceItem.Item.IsUnique)
                                                {
                                                    if (sourceInventory.Key.Item1 != seenInventoryLocation.Item1 ||
                                                        sourceItem.SortedCategory != seenInventoryLocation.Item2)
                                                    {
                                                        if (filter.InActiveInventories(activeCharacter, activeRetainer,
                                                            sourceInventory.Key.Item1, seenInventoryLocation.Item1))
                                                        {
                                                            PluginLog.Verbose(
                                                                "Added item to filter result as we've seen the item before: " +
                                                                sourceItem.FormattedName);
                                                            sortedItems.Add(new SortingResult(sourceInventory.Key.Item1,
                                                                seenInventoryLocation.Item1, sourceItem.SortedContainer,
                                                                seenInventoryLocation.Item2, sourceItem,
                                                                (int) sourceItem.TempQuantity));
                                                        }

                                                        sourceItem.TempQuantity -= sourceItem.TempQuantity;
                                                    }
                                                }

                                                break;
                                            }
                                        }

                                        seenInventoryLocations.Remove(seenInventoryLocation);
                                    }
                                }
                            }
                        }

                        if (filter.DuplicatesOnly == true)
                        {
                            continue;
                        }

                        if (sourceItem.TempQuantity == 0)
                        {
                            continue;
                        }

                        if (slotsAvailable.Count == 0)
                        {
                            break;
                        }

                        var nextSlot = slotsAvailable.First();
                        while (nextSlot.Value == 0 && slotsAvailable.Count != 0)
                        {
                            slotsAvailable.Remove(nextSlot.Key);
                            if (slotsAvailable.Count == 0)
                            {
                                break;
                            }

                            nextSlot = slotsAvailable.First();
                        }

                        if (nextSlot.Key.Item1 == sourceItem.RetainerId)
                        {
                            continue;
                        }

                        //Don't compare inventory to itself
                        if (nextSlot.Value != 0)
                        {
                            if (filter.InActiveInventories(activeCharacter, activeRetainer, sourceInventory.Key.Item1,
                                nextSlot.Key.Item1))
                            {
                                //This check stops the item from being sorted into it's own bag, this generally means its already in the optimal place
                                if (sourceInventory.Key.Item1 != nextSlot.Key.Item1 ||
                                    sourceItem.SortedCategory != nextSlot.Key.Item2)
                                {
                                    PluginLog.Verbose("Added item to filter result in next available slot: " +
                                                      sourceItem.FormattedName);
                                    sortedItems.Add(new SortingResult(sourceInventory.Key.Item1, nextSlot.Key.Item1,
                                        sourceItem.SortedContainer, nextSlot.Key.Item2, sourceItem,
                                        (int) sourceItem.TempQuantity));
                                }
                            }

                            slotsAvailable[nextSlot.Key] = nextSlot.Value - 1;
                        }
                        else
                        {
                            PluginLog.Verbose("Added item to unsortable list, maybe I should show these somewhere: " +
                                              sourceItem.FormattedName);
                            unsortableItems.Add(sourceItem);
                        }
                    }
                }
            }
            else
            {
                //Determine which source and destination inventories we actually need to examine
                Dictionary<(ulong, InventoryCategory), List<InventoryItem>> sourceInventories = new();
                foreach (var character in inventories)
                {
                    foreach (var inventory in character.Value)
                    {
                        var inventoryKey = (character.Key, inventory.Key);
                        if (filter.SourceAllRetainers.HasValue && filter.SourceAllRetainers.Value && PluginLogic.CharacterMonitor.IsRetainer(character.Key) && (displayCrossCharacter || PluginLogic.CharacterMonitor.BelongsToActiveCharacter(character.Key)))
                        {
                            if (!sourceInventories.ContainsKey(inventoryKey))
                            {
                                sourceInventories.Add(inventoryKey, inventory.Value);
                            }
                        }
                        if (filter.SourceAllCharacters.HasValue && filter.SourceAllCharacters.Value && (displayCrossCharacter || PluginLogic.CharacterMonitor.ActiveCharacter == character.Key))
                        {
                            if (!sourceInventories.ContainsKey(inventoryKey))
                            {
                                sourceInventories.Add(inventoryKey, inventory.Value);
                            }
                        }
                        if (filter.SourceInventories.Contains(inventoryKey) && (displayCrossCharacter || PluginLogic.CharacterMonitor.BelongsToActiveCharacter(character.Key)))
                        {
                            if (!sourceInventories.ContainsKey(inventoryKey))
                            {
                                sourceInventories.Add(inventoryKey, inventory.Value);
                            }
                        }
                    }
                }

                HashSet<int> distinctItems = new HashSet<int>();
                HashSet<int> duplicateItems = new HashSet<int>();

                //Filter the source and destination inventories based on the applicable items so we have less to sort
                Dictionary<(ulong, InventoryCategory), List<InventoryItem>> filteredSources = new();
                //Dictionary<(ulong, InventoryCategory), List<InventoryItem>> filteredDestinations = new();
                PluginLog.Verbose(sourceInventories.Count() + " inventories to examine.");
                foreach (var sourceInventory in sourceInventories)
                {
                    if (!filteredSources.ContainsKey(sourceInventory.Key))
                    {
                        filteredSources.Add(sourceInventory.Key, new List<InventoryItem>());
                    }

                    filteredSources[sourceInventory.Key].AddRange(sourceInventory.Value.Where(filter.FilterItem));
                }
                if (filter.DuplicatesOnly.HasValue && filter.DuplicatesOnly == true)
                {
                    foreach (var filteredSource in filteredSources)
                    {
                        foreach (var item in filteredSource.Value)
                        {
                            var hashCode = item.GetHashCode();
                            if (distinctItems.Contains(hashCode))
                            {
                                if (!duplicateItems.Contains(hashCode))
                                {
                                    duplicateItems.Add(hashCode);
                                }
                            }
                            else
                            {
                                distinctItems.Add(hashCode);
                            }
                        }
                    }
                }

                foreach (var filteredSource in filteredSources)
                {
                    foreach (var item in filteredSource.Value)
                    {
                        if (filter.DuplicatesOnly.HasValue && filter.DuplicatesOnly == true)
                        {
                            if (duplicateItems.Contains(item.GetHashCode()))
                            {
                                sortedItems.Add(new SortingResult(filteredSource.Key.Item1, item.SortedContainer,
                                    item, (int)item.Quantity));
                            }

                        }
                        else
                        {
                            sortedItems.Add(new SortingResult(filteredSource.Key.Item1, item.SortedContainer,
                                item, (int)item.Quantity));    
                        }
                        
                    }
                }
            }

            return new FilterResult(sortedItems, unsortableItems);
        }
    }
}