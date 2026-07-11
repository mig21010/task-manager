import { useState } from 'react'
import TaskList from './components/TaskList'
import TaskForm from './components/TaskForm'

function App() {

  const [ refresh, setRefresh ] = useState(0);
  return (
    <div className="min-h-screen bg-gray-100 p-8 max-w-2xl mx-auto">
      <h1 className="text-3xl font-bold text-blue-600">
        Task Manager
      </h1>
      <TaskForm onTaskCreated={() => setRefresh(r => r + 1)} />
      <TaskList key={refresh}/> 
    </div>
  )
}

export default App