using System.Collections.Generic;

using static CitizenFX.Core.Native.API;

namespace vMenuClient.data
{
    public static class BlipInfo
    {
        // Sprites that still make sense turned to face the player's heading: the on-foot dot, the generic
        // car vMenu gives any unlisted vehicle (225), planes, boats and submarines. The rest are drawn
        // upright or are already turned by the game, and rotating them makes them spin or jitter.
        private static readonly HashSet<int> rotatingSprites = new() { 1, 225, 423, 427, 760 };
        private static readonly Dictionary<uint, bool> rotatesByModel = new();

        /// <summary>
        /// Whether a player blip with this sprite should follow the player's heading. Model 0 means on foot.
        /// </summary>
        public static bool ShouldRotateBlip(int sprite, uint model)
        {
            if (!rotatingSprites.Contains(sprite))
            {
                return false;
            }

            if (model == 0)
            {
                return true;
            }

            if (rotatesByModel.TryGetValue(model, out var wanted))
            {
                return wanted;
            }

            // Motorcycles (class 8), bicycles (class 13) and quads: the game already orients those itself.
            var vehicleClass = GetVehicleClassFromName(model);
            wanted = vehicleClass != 8
                && vehicleClass != 13
                && !IsThisModelAQuadbike(model)
                && !IsThisModelAnAmphibiousQuadbike(unchecked((int)model));

            rotatesByModel[model] = wanted;
            return wanted;
        }

        public static int GetBlipSpriteForVehicle(int vehicle)
        {
            var model = (uint)GetEntityModel(vehicle);
            var sprites = new Dictionary<uint, int>()
            {
                { (uint)GetHashKey("taxi"), 56 },
                //
                { (uint)GetHashKey("nightshark"), 225 },
                //
                { (uint)GetHashKey("rhino"), 421 },
                //
                { (uint)GetHashKey("lazer"), 424 },
                { (uint)GetHashKey("besra"), 424 },
                { (uint)GetHashKey("hydra"), 424 },
                //
                { (uint)GetHashKey("insurgent"), 426 },
                { (uint)GetHashKey("insurgent2"), 426 },
                { (uint)GetHashKey("insurgent3"), 426 },
                //
                { (uint)GetHashKey("limo2"), 460 },
                //
                { (uint)GetHashKey("blazer5"), 512 },
                //
                { (uint)GetHashKey("phantom2"), 528 },
                { (uint)GetHashKey("boxville5"), 529 },
                { (uint)GetHashKey("ruiner2"), 530 },
                { (uint)GetHashKey("dune4"), 531 },
                { (uint)GetHashKey("dune5"), 531 },
                { (uint)GetHashKey("wastelander"), 532 },
                { (uint)GetHashKey("voltic2"), 533 },
                { (uint)GetHashKey("technical2"), 534 },
                { (uint)GetHashKey("technical3"), 534 },
                { (uint)GetHashKey("technical"), 534 },
                //
                { (uint)GetHashKey("apc"), 558 },
                { (uint)GetHashKey("oppressor"), 559 },
                { (uint)GetHashKey("oppressor2"), 559 },
                { (uint)GetHashKey("halftrack"), 560 },
                { (uint)GetHashKey("dune3"), 561 },
                { (uint)GetHashKey("tampa3"), 562 },
                { (uint)GetHashKey("trailersmall2"), 563 },
                //
                { (uint)GetHashKey("alphaz1"), 572 },
                { (uint)GetHashKey("bombushka"), 573 },
                { (uint)GetHashKey("havok"), 574 },
                { (uint)GetHashKey("howard"), 575 },
                { (uint)GetHashKey("hunter"), 576 },
                { (uint)GetHashKey("microlight"), 577 },
                { (uint)GetHashKey("mogul"), 578 },
                { (uint)GetHashKey("molotok"), 579 },
                { (uint)GetHashKey("nokota"), 580 },
                { (uint)GetHashKey("pyro"), 581 },
                { (uint)GetHashKey("rogue"), 582 },
                { (uint)GetHashKey("starling"), 583 },
                { (uint)GetHashKey("seabreeze"), 584 },
                { (uint)GetHashKey("tula"), 585 },
                //
                { (uint)GetHashKey("avenger"), 589 },
                //
                { (uint)GetHashKey("stromberg"), 595 },
                { (uint)GetHashKey("deluxo"), 596 },
                { (uint)GetHashKey("thruster"), 597 },
                { (uint)GetHashKey("khanjali"), 598 },
                { (uint)GetHashKey("riot2"), 599 },
                { (uint)GetHashKey("volatol"), 600 },
                { (uint)GetHashKey("barrage"), 601 },
                { (uint)GetHashKey("akula"), 602 },
                { (uint)GetHashKey("chernobog"), 603 },
            };

            if (sprites.ContainsKey(model))
            {
                return sprites[model];
            }
            else if (IsThisModelABike(model))
            {
                return 348;
            }
            else if (IsThisModelABoat(model))
            {
                return 427;
            }
            else if (IsThisModelAHeli(model))
            {
                return 422;
            }
            else if (IsThisModelAPlane(model))
            {
                return 423;
            }
            return 225;
        }
    }
}
