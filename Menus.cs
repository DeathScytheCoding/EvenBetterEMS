using RAGENativeUI;
using RAGENativeUI.PauseMenu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvenBetterEMS
{
    internal static class Menus
    {
        private static MenuPool _menuPool;

        public static TabView hospitalMenu;

        public static TabSubmenuItem pendingResultsList;
        public static TabSubmenuItem publishedResultsList; 
    }
}
