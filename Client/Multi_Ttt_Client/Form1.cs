namespace Multi_Ttt_Client;

public partial class Form1 : Form
{
    // 버튼 9개를 배열로 관리 (기획자로서 통제하기 쉽게!)
    private Button[] btnBoard = new Button[9];

    public Form1()
    {
        InitializeComponent();
        CreateBoard();
    }

    private void CreateBoard()
    {
        for (int i = 0; i < 9; i++)
        {
            btnBoard[i] = new Button();
            btnBoard[i].Size = new Size(100, 100);
            // 격자 배치 로직: (i % 3)은 가로 위치, (i / 3)은 세로 위치
            btnBoard[i].Location = new Point(10 + (i % 3) * 105, 10 + (i / 3) * 105);
            btnBoard[i].Tag = i; // 몇 번째 버튼인지 인덱스 저장
            btnBoard[i].Text = (i + 1).ToString(); // 초기에는 숫자 표시
            btnBoard[i].Click += OnCellClick; // 클릭 이벤트 연결

            this.Controls.Add(btnBoard[i]);
        }
        this.ClientSize = new Size(330, 330); // 폼 크기 조절
    }

    private void OnCellClick(object? sender, EventArgs e)
    {
        Button clickedBtn = (Button)sender!;
        int index = (int)clickedBtn.Tag;

        // 서버 전송 로직이 들어갈 자리
        MessageBox.Show($"{index + 1}번 칸을 눌렀습니다!");
    }
}