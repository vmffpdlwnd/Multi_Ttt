using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SocketIOClient;
using System.Text.Json;

namespace Multi_Ttt_Client
{
    public partial class Form1 : Form
    {
        // 게임 모드 및 상태 변수
        private enum GameMode { None, LocalMulti, VsAI, ServerMulti }
        private GameMode currentMode = GameMode.None;

        private Button[] btnBoard = new Button[9];
        private int[] board = new int[9]; // 0: 빈칸, 1: P1(O), 2: P2(X)
        private Queue<int> historyP1 = new Queue<int>();
        private Queue<int> historyP2 = new Queue<int>();

        private int currentPlayer = 1;
        private bool isGameOver = false;
        private Random aiRandom = new Random();

        // 서버 통신 관련 변수
        private SocketIOClient.SocketIO socket;
        private int myPlayerNumber = 0; // 서버에서 할당받는 플레이어 번호

        public Form1()
        {
            this.Text = "Multi_Ttt - TicTacToe Project";
            this.ClientSize = new Size(330, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            ShowMenu();
        }

        // 메인 메뉴 UI 생성
        private void ShowMenu()
        {
            this.Controls.Clear();

            Label lblTitle = new Label 
            { 
                Text = "무한 틱택토", 
                Font = new Font("맑은 고딕", 20, FontStyle.Bold), 
                Dock = DockStyle.Top, 
                Height = 80, 
                TextAlign = ContentAlignment.MiddleCenter 
            };

            Button btnLocal = new Button { Text = "로컬 멀티플레이", Dock = DockStyle.Top, Height = 60 };
            btnLocal.Click += (s, e) => StartGame(GameMode.LocalMulti);

            Button btnAI = new Button { Text = "AI 대전 (싱글)", Dock = DockStyle.Top, Height = 60 };
            btnAI.Click += (s, e) => StartGame(GameMode.VsAI);

            Button btnServer = new Button { Text = "서버 멀티플레이", Dock = DockStyle.Top, Height = 60 };
            btnServer.Click += (s, e) => StartGame(GameMode.ServerMulti);

            this.Controls.Add(btnServer);
            this.Controls.Add(btnAI);
            this.Controls.Add(btnLocal);
            this.Controls.Add(lblTitle);
        }

        // 게임 시작 및 모드 설정
        private async void StartGame(GameMode mode)
        {
            currentMode = mode;
            this.Controls.Clear();
            InitializeBoard();

            if (mode == GameMode.ServerMulti)
            {
                await ConnectToServer();
            }
        }

        // 3x3 바둑판 생성
        private void InitializeBoard()
        {
            for (int i = 0; i < 9; i++)
            {
                btnBoard[i] = new Button();
                btnBoard[i].Size = new Size(100, 100);
                btnBoard[i].Location = new Point(10 + (i % 3) * 105, 10 + (i / 3) * 105);
                btnBoard[i].Tag = i;
                btnBoard[i].Font = new Font("Arial", 24, FontStyle.Bold);
                btnBoard[i].Click += OnCellClick;
                this.Controls.Add(btnBoard[i]);
            }

            Button btnBack = new Button { Text = "메뉴로", Dock = DockStyle.Bottom, Height = 40 };
            btnBack.Click += async (s, e) => 
            { 
                if (socket != null && socket.Connected) await socket.DisconnectAsync();
                ResetGameData(); 
                ShowMenu(); 
            };
            this.Controls.Add(btnBack);

            RefreshBoard();
        }

        // 오라클 서버 연결 및 이벤트 리스너 등록
        private async Task ConnectToServer()
        {
            socket = new SocketIOClient.SocketIO("http://158.179.161.105:3000");

            socket.OnConnected += (sender, e) => {
                this.Invoke((MethodInvoker)delegate { this.Text = "서버 연결됨"; });
            };

            // 서버로부터 플레이어 번호(1 or 2) 할당받음
            socket.On("player_assigned", response => {
                myPlayerNumber = response.GetValue<int>();
            });

            // 서버의 게임 상태를 클라이언트에 동기화
            socket.On("update_game", response => {
                var data = response.GetValue<JsonElement>();
                int[] serverBoard = data.GetProperty("board").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                int serverTurn = data.GetProperty("turn").GetInt32();
                int winner = data.GetProperty("winner").GetInt32();

                this.Invoke((MethodInvoker)delegate {
                    this.board = serverBoard;
                    this.currentPlayer = serverTurn;
                    RefreshBoard();
                    
                    if (winner != 0) {
                        MessageBox.Show($"플레이어 {winner} 승리!");
                        ResetGameData();
                        ShowMenu();
                    }
                });
            });

            try {
                await socket.ConnectAsync();
            } catch (Exception ex) {
                MessageBox.Show("서버 연결 실패: " + ex.Message);
                ShowMenu();
            }
        }

        // 버튼 클릭 시 호출 (모드별 분기)
        private void OnCellClick(object sender, EventArgs e)
        {
            if (isGameOver) return;

            Button clickedBtn = (Button)sender;
            int index = (int)clickedBtn.Tag;

            if (board[index] != 0) return;

            if (currentMode == GameMode.ServerMulti)
            {
                // 서버 모드: 서버에 착수 요청만 보냄
                if (currentPlayer != myPlayerNumber) return;
                socket.EmitAsync("place_stone", index);
            }
            else
            {
                // 로컬/AI 모드: 직접 계산
                if (currentMode == GameMode.VsAI && currentPlayer == 2) return;
                ProcessTurn(index);

                if (currentMode == GameMode.VsAI && !isGameOver && currentPlayer == 2)
                {
                    System.Windows.Forms.Timer aiTimer = new System.Windows.Forms.Timer { Interval = 500 };
                    aiTimer.Tick += (s, ev) => {
                        aiTimer.Stop();
                        DoAIMove();
                    };
                    aiTimer.Start();
                }
            }
        }

        // 턴 처리 및 4번째 돌 소멸 로직 (로컬 전용)
        private void ProcessTurn(int index)
        {
            var currentHistory = (currentPlayer == 1) ? historyP1 : historyP2;

            // 3개가 이미 놓여있으면 가장 오래된 돌 제거
            if (currentHistory.Count >= 3)
            {
                int oldIndex = currentHistory.Dequeue();
                board[oldIndex] = 0;
            }

            board[index] = currentPlayer;
            currentHistory.Enqueue(index);

            if (CheckWinner(currentPlayer))
            {
                RefreshBoard();
                MessageBox.Show($"플레이어 {currentPlayer} 승리!");
                isGameOver = true;
                return;
            }

            currentPlayer = (currentPlayer == 1) ? 2 : 1;
            RefreshBoard();
        }

        // AI 행동 로직 (공격/방어/랜덤)
        private void DoAIMove()
        {
            int winMove = FindBestMove(2); // 1순위: 공격
            if (winMove != -1 && aiRandom.Next(100) < 80)
            {
                ProcessTurn(winMove);
                return;
            }

            int blockMove = FindBestMove(1); // 2순위: 방어
            if (blockMove != -1 && aiRandom.Next(100) < 60)
            {
                ProcessTurn(blockMove);
                return;
            }

            if (board[4] == 0 && aiRandom.Next(100) < 50) // 3순위: 중앙 선점
            {
                ProcessTurn(4);
                return;
            }

            // 4순위: 빈칸 중 랜덤
            var emptyCells = board.Select((val, idx) => new { val, idx }).Where(x => x.val == 0).Select(x => x.idx).ToList();
            if (emptyCells.Count > 0)
            {
                ProcessTurn(emptyCells[aiRandom.Next(emptyCells.Count)]);
            }
        }

        // 승리 또는 방어 가능한 위치 탐색
        private int FindBestMove(int player)
        {
            int[,] winCases = { {0,1,2}, {3,4,5}, {6,7,8}, {0,3,6}, {1,4,7}, {2,5,8}, {0,4,8}, {2,4,6} };
            for (int i = 0; i < 8; i++)
            {
                int c1 = winCases[i, 0]; int c2 = winCases[i, 1]; int c3 = winCases[i, 2];
                if (board[c1] == player && board[c2] == player && board[c3] == 0) return c3;
                if (board[c1] == player && board[c3] == player && board[c2] == 0) return c2;
                if (board[c2] == player && board[c3] == player && board[c1] == 0) return c1;
            }
            return -1;
        }

        // 보드 UI 갱신 (소멸 예고 포함)
        private void RefreshBoard()
        {
            for (int i = 0; i < 9; i++)
            {
                if (board[i] == 0)
                {
                    btnBoard[i].Text = (i + 1).ToString();
                    btnBoard[i].BackColor = Color.White;
                    btnBoard[i].ForeColor = Color.LightGray;
                }
                else
                {
                    btnBoard[i].Text = (board[i] == 1) ? "O" : "X";
                    btnBoard[i].ForeColor = Color.White;
                    btnBoard[i].BackColor = (board[i] == 1) ? Color.DodgerBlue : Color.Crimson;
                }

                // 다음에 사라질 돌 주황색 예고 (로컬/AI 모드에서만 작동)
                if (currentMode != GameMode.ServerMulti)
                {
                    if (currentPlayer == 1 && historyP1.Count == 3 && i == historyP1.Peek()) btnBoard[i].BackColor = Color.Orange;
                    if (currentPlayer == 2 && historyP2.Count == 3 && i == historyP2.Peek()) btnBoard[i].BackColor = Color.Orange;
                }
            }
        }

        // 승리 조건 판정
        private bool CheckWinner(int player)
        {
            int[,] winCases = { {0,1,2}, {3,4,5}, {6,7,8}, {0,3,6}, {1,4,7}, {2,5,8}, {0,4,8}, {2,4,6} };
            for (int i = 0; i < 8; i++)
            {
                if (board[winCases[i, 0]] == player && board[winCases[i, 1]] == player && board[winCases[i, 2]] == player) return true;
            }
            return false;
        }

        // 게임 데이터 초기화
        private void ResetGameData()
        {
            board = new int[9];
            historyP1.Clear();
            historyP2.Clear();
            currentPlayer = 1;
            isGameOver = false;
            myPlayerNumber = 0;
        }
    }
}