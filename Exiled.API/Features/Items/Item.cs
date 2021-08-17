// -----------------------------------------------------------------------
// <copyright file="Item.cs" company="Exiled Team">
// Copyright (c) Exiled Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------

namespace Exiled.API.Features.Items
{
    using System.Collections.Generic;
    using System.Linq;

    using Exiled.API.Extensions;

    using InventorySystem;
    using InventorySystem.Items;
    using InventorySystem.Items.Armor;
    using InventorySystem.Items.Firearms;
    using InventorySystem.Items.Firearms.Ammo;
    using InventorySystem.Items.Keycards;
    using InventorySystem.Items.MicroHID;
    using InventorySystem.Items.Pickups;
    using InventorySystem.Items.Radio;
    using InventorySystem.Items.ThrowableProjectiles;
    using InventorySystem.Items.Usables;

    using Mirror;

    using UnityEngine;

    /// <summary>
    /// A wrapper class for <see cref="ItemBase"/>.
    /// </summary>
    public class Item
    {
        /// <summary>
        /// A dictionary of all <see cref="ItemBase"/>'s that have been converted into <see cref="Item"/>.
        /// </summary>
        internal static readonly Dictionary<ItemBase, Item> BaseToItem = new Dictionary<ItemBase, Item>();

        /// <summary>
        /// A dictionary of all <see cref="Serial"/>s that have been assigned to an item.
        /// </summary>
        internal static readonly Dictionary<ushort, Item> SerialToItem = new Dictionary<ushort, Item>();

        private ushort id;

        /// <summary>
        /// Initializes a new instance of the <see cref="Item"/> class.
        /// </summary>
        /// <param name="itemBase"><inheritdoc cref="Base"/></param>
        public Item(ItemBase itemBase)
        {
            Base = itemBase;
            Type = itemBase.ItemTypeId;
            Serial = Base.OwnerInventory.UserInventory.Items.FirstOrDefault(i => i.Value == Base).Key;
            BaseToItem.Add(itemBase, this);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Item"/> class.
        /// </summary>
        /// <param name="type"><inheritdoc cref="Type"/></param>
        public Item(ItemType type)
        {
            if (!InventoryItemLoader.AvailableItems.TryGetValue(type, out ItemBase itemBase))
                return;

            Base = itemBase;
            Type = itemBase.ItemTypeId;
            Serial = itemBase.PickupDropModel.NetworkInfo.Serial;
            BaseToItem.Add(itemBase, this);
        }

        /// <summary>
        /// Gets the unique serial number for the item.
        /// </summary>
        public ushort Serial
        {
            get
            {
                id = Base.OwnerInventory.UserInventory.Items.FirstOrDefault(i => i.Value == Base).Key;
                return id;
            }

            internal set
            {
                if (value == 0)
                {
                    value = ItemSerialGenerator.GenerateNext();
                }

                Base.PickupDropModel.Info.Serial = value;
                Base.PickupDropModel.NetworkInfo = Base.PickupDropModel.Info;
                id = value;
            }
        }

        /// <summary>
        /// Gets or sets the scale for the item.
        /// </summary>
        public Vector3 Scale
        {
            get => Base.PickupDropModel.gameObject.transform.localScale;
            set => Base.PickupDropModel.gameObject.transform.localScale = value;
        }

        /// <summary>
        /// Gets the <see cref="ItemBase"/> of the item.
        /// </summary>
        public ItemBase Base { get; }

        /// <summary>
        /// Gets the <see cref="ItemType"/> of the item.
        /// </summary>
        public ItemType Type { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Player"/> who owns the item.
        /// </summary>
        public Player Owner => Player.Get(Base.Owner);

        /// <summary>
        /// Gets an existing <see cref="Item"/> or creates a new instance of one.
        /// </summary>
        /// <param name="itemBase">The <see cref="ItemBase"/> to convert into an item.</param>
        /// <returns>The item wrapper for the given <see cref="ItemBase"/>.</returns>
        public static Item Get(ItemBase itemBase)
        {
            if (itemBase == null)
                return null;

            if (BaseToItem.ContainsKey(itemBase))
                return BaseToItem[itemBase];

            switch (itemBase)
            {
                case InventorySystem.Items.Firearms.Firearm firearm:
                    return new Firearm(firearm);
                case KeycardItem keycard:
                    return new Keycard(keycard);
                case UsableItem usable:
                    return new Usable(usable);
                case RadioItem radio:
                    return new Radio(radio);
                case MicroHIDItem micro:
                    return new MicroHid(micro);
                case BodyArmor armor:
                    return new Armor(armor);
                case AmmoItem ammo:
                    return new Ammo(ammo);
                case ThrowableItem throwable:
                    switch (throwable.Projectile)
                    {
                        case FlashbangGrenade _:
                            return new FlashGrenade(throwable);
                        case ExplosionGrenade _:
                            return new ExplosiveGrenade(throwable);
                        default:
                            return new Throwable(throwable);
                    }

                default:
                    return new Item(itemBase);
            }
        }

        /// <summary>
        /// Gives this item to a <see cref="Player"/>.
        /// </summary>
        /// <param name="player">The <see cref="Player"/> to give the item to.</param>
        public void Give(Player player) => player.AddItem(Base);

        /// <summary>
        /// Spawns the item on the map.
        /// </summary>
        /// <param name="position">The location to spawn the item.</param>
        /// <param name="rotation">The rotation of the item.</param>
        /// <returns>The <see cref="Pickup"/> created by spawning this item.</returns>
        public Pickup Spawn(Vector3 position, Quaternion rotation)
        {
            if (Base.PickupDropModel.Info.ItemId == ItemType.None)
                Base.PickupDropModel.Info.ItemId = Type;
            Base.PickupDropModel.Info.Position = position;
            Base.PickupDropModel.Info.Rotation = new LowPrecisionQuaternion(rotation);
            Base.PickupDropModel.NetworkInfo = Base.PickupDropModel.Info;

            ItemPickupBase ipb = Object.Instantiate(Base.PickupDropModel, position, rotation);
            if (ipb is FirearmPickup firearmPickup)
            {
                firearmPickup.Status = new FirearmStatus(((Firearm)this).MaxAmmo, firearmPickup.Status.Flags, firearmPickup.Status.Attachments);
                firearmPickup.NetworkStatus = firearmPickup.Status;
            }

            NetworkServer.Spawn(ipb.gameObject);
            ipb.InfoReceived(default, Base.PickupDropModel.NetworkInfo);
            Pickup pickup = Pickup.Get(ipb);
            pickup.Scale = Scale;
            return pickup;
        }
    }
}
