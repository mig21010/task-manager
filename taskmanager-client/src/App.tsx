import TaskList from './components/TaskList'

function App() {
  return (
    <div className="min-h-screen bg-gray-100 p-8">
      <h1 className="text-3xl font-bold text-blue-600">
        Task Manager
      </h1>
      <TaskList />
    </div>
  )
}

export default App