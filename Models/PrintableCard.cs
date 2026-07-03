using MtgCommanderBuilder.ViewModels;

namespace MtgCommanderBuilder.Models
{
    public class PrintableCard : ViewModelBase
    {
        private Card _card = null!;
        private bool _isSelected = true;
        private int _printQuantity = 1;

        public Card Card
        {
            get => _card;
            set => SetProperty(ref _card, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int PrintQuantity
        {
            get => _printQuantity;
            set
            {
                if (value < 0) value = 0;
                if (SetProperty(ref _printQuantity, value))
                {
                    // Automatically uncheck if quantity drops to 0, or check if it increases above 0
                    if (value == 0)
                    {
                        IsSelected = false;
                    }
                    else if (value > 0 && !IsSelected)
                    {
                        IsSelected = true;
                    }
                }
            }
        }

        public PrintableCard(Card card)
        {
            Card = card;
            PrintQuantity = card.Quantity;
        }
    }
}
